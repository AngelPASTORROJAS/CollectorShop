-- =========================================================================
-- MIGRATION : V1_0_0__init_finance_and_ledger.sql
-- CIBLE     : finance_db (Bulle Haute Sécurité)
-- SYNCHRO   : Transactions + Grand Livre Comptable
-- =========================================================================

-- Création des types énumérés exclusifs au domaine Finance
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'transaction_status') THEN
        CREATE TYPE transaction_status AS ENUM ('PENDING', 'COMPLETED', 'REFUNDED');
    END IF;
    
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'ledger_account_type') THEN
        CREATE TYPE ledger_account_type AS ENUM ('SYSTEM_REVENUE', 'SELLER_PAYOUT', 'BUYER_HOLD');
    END IF;

    -- AJOUT : Type énuméré pour les devises (Norme ISO 4217)
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'currency_type') THEN
        CREATE TYPE currency_type AS ENUM ('EUR', 'USD', 'GBP', 'CHF');
    END IF;
END $$;

-- 1. Table des transactions de trading (Financier)
CREATE TABLE IF NOT EXISTS financial_transactions (
    transaction_id BIGSERIAL PRIMARY KEY,
    buyer_id BIGINT NOT NULL,  -- Logique users.id (DbUsers)
    seller_id BIGINT NOT NULL, -- Logique users.id (DbUsers)
    item_id BIGINT NOT NULL,   -- Logique items.id (DbCollector)
    
    gross_amount NUMERIC(12, 2) NOT NULL,
    commission_amount NUMERIC(12, 2) NOT NULL,
    
    -- Utilisation de l'ENUM pour la devise (Zéro allocation de chaîne de caractères)
    currency currency_type DEFAULT 'EUR' NOT NULL,
    status transaction_status DEFAULT 'PENDING' NOT NULL,
    
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_transactions_status ON financial_transactions(status);
CREATE INDEX IF NOT EXISTS idx_transactions_buyer ON financial_transactions(buyer_id);
CREATE INDEX IF NOT EXISTS idx_transactions_seller ON financial_transactions(seller_id);
CREATE INDEX IF NOT EXISTS idx_transactions_item ON financial_transactions(item_id);

-- 2. Grand Livre Comptable Immuable (Ledger)
CREATE TABLE IF NOT EXISTS financial_ledger (
    ledger_id BIGSERIAL PRIMARY KEY,
    transaction_id BIGINT REFERENCES financial_transactions(transaction_id) ON DELETE RESTRICT,
    account_type ledger_account_type NOT NULL,
    debit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    credit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    description VARCHAR(255) NOT NULL,
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_ledger_recorded_at ON financial_ledger(recorded_at);
CREATE INDEX IF NOT EXISTS idx_ledger_transaction ON financial_ledger(transaction_id);

-- =========================================================================
-- Fonctions & Déclencheurs (Triggers)
-- =========================================================================

-- 1. Fonction générique de mise à jour temporelle
CREATE OR REPLACE FUNCTION trigger_set_timestamp()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- 2. Application du trigger sur la table financial_transactions
CREATE OR REPLACE TRIGGER set_timestamp_financial_transactions
    BEFORE UPDATE ON financial_transactions
    FOR EACH ROW
    EXECUTE FUNCTION trigger_set_timestamp();


-- =========================================================================
-- Procédures stockées
-- =========================================================================

CREATE OR REPLACE FUNCTION sp_process_trade(
    p_buyer_id BIGINT,
    p_seller_id BIGINT,
    p_item_id BIGINT,
    p_gross_amount NUMERIC(12, 2),
    p_currency currency_type DEFAULT 'EUR'
)
RETURNS BIGINT AS $$
DECLARE
    v_transaction_id BIGINT;
    v_commission_amount NUMERIC(12, 2);
    v_net_seller_amount NUMERIC(12, 2);
BEGIN
    -- 1. Sécurité réglementaire : Pas d'auto-achat
    IF p_buyer_id = p_seller_id THEN
        RAISE EXCEPTION 'Un utilisateur ne peut pas acheter son propre objet.';
    END IF;

    IF p_gross_amount <= 0 THEN
        RAISE EXCEPTION 'Le montant de la transaction doit être supérieur à zéro.';
    END IF;

    -- 2. Calculs financiers (Commission fixe de 5%)
    v_commission_amount := ROUND(p_gross_amount * 0.05, 2);
    v_net_seller_amount := p_gross_amount - v_commission_amount;

    -- 3. Insertion de la transaction principale (Statut COMPLETED directement si paiement immédiat)
    INSERT INTO financial_transactions (
        buyer_id, seller_id, item_id, gross_amount, commission_amount, currency, status
    ) VALUES (
        p_buyer_id, p_seller_id, p_item_id, p_gross_amount, v_commission_amount, p_currency, 'COMPLETED'
    )
    RETURNING transaction_id INTO v_transaction_id;

    -- 4. ÉCRITURES DOUBLE-ENTRÉE DANS LE LEDGER IMMUABLE
    
    -- A. Trace du flux de l'acheteur (Montant brut total sortant)
    INSERT INTO financial_ledger (transaction_id, account_type, debit_amount, credit_amount, description)
    VALUES (v_transaction_id, 'BUYER_HOLD', p_gross_amount, 0.00, 'Achat Item #' || p_item_id || ' par User #' || p_buyer_id);

    -- B. Crédit du vendeur (Montant net après commission)
    INSERT INTO financial_ledger (transaction_id, account_type, debit_amount, credit_amount, description)
    VALUES (v_transaction_id, 'SELLER_PAYOUT', 0.00, v_net_seller_amount, 'Paiement vendeur pour Vente Item #' || p_item_id);

    -- C. Revenu de la plateforme (La commission de 5%)
    INSERT INTO financial_ledger (transaction_id, account_type, debit_amount, credit_amount, description)
    VALUES (v_transaction_id, 'SYSTEM_REVENUE', 0.00, v_commission_amount, 'Commission plateforme 5% sur Transaction #' || v_transaction_id);

    -- Renvoie l'ID de la transaction créée pour l'API C#
    RETURN v_transaction_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_transaction_with_ledger(p_transaction_id BIGINT)
RETURNS TABLE (
    transaction_id BIGINT,
    buyer_id BIGINT,
    seller_id BIGINT,
    item_id BIGINT,
    gross_amount NUMERIC(12, 2),
    commission_amount NUMERIC(12, 2),
    currency currency_type,
    status transaction_status,
    created_at TIMESTAMP WITH TIME ZONE,
    ledger_id BIGINT,
    account_type ledger_account_type,
    debit_amount NUMERIC(12, 2),
    credit_amount NUMERIC(12, 2),
    ledger_description VARCHAR(255),
    recorded_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        t.transaction_id,
        t.buyer_id,
        t.seller_id,
        t.item_id,
        t.gross_amount,
        t.commission_amount,
        t.currency,
        t.status,
        t.created_at,
        l.ledger_id,
        l.account_type,
        l.debit_amount,
        l.credit_amount,
        l.description AS ledger_description,
        l.recorded_at
    FROM financial_transactions t
    LEFT JOIN financial_ledger l ON t.transaction_id = l.transaction_id
    WHERE t.transaction_id = p_transaction_id
    ORDER BY l.ledger_id ASC; -- Ordonné pour suivre le flux logique comptable
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_get_user_transactions_history(p_user_id BIGINT)
RETURNS TABLE (
    transaction_id BIGINT,
    buyer_id BIGINT,
    seller_id BIGINT,
    item_id BIGINT,
    gross_amount NUMERIC(12, 2),
    commission_amount NUMERIC(12, 2),
    currency currency_type,
    status transaction_status,
    created_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    -- Partie 1 : Récupération des ACHATS (Utilise pleinement idx_transactions_buyer)
    SELECT t.transaction_id, t.buyer_id, t.seller_id, t.item_id, t.gross_amount, t.commission_amount, t.currency, t.status, t.created_at
    FROM financial_transactions t
    WHERE t.buyer_id = p_user_id

    UNION ALL

    -- Partie 2 : Récupération des VENTES (Utilise pleinement idx_transactions_seller)
    SELECT t.transaction_id, t.buyer_id, t.seller_id, t.item_id, t.gross_amount, t.commission_amount, t.currency, t.status, t.created_at
    FROM financial_transactions t
    WHERE t.seller_id = p_user_id

    -- Tri global sur le résultat fusionné (Le plus récent en premier)
    ORDER BY created_at DESC;
END;
$$ LANGUAGE plpgsql;