-- =========================================================================
-- MIGRATION : V1_0_0__init_finance_and_ledger.sql
-- CIBLE     : finance_db (Bulle Haute Sécurité)
-- ANALYTICS : Optimisé à 100% pour la BI et les requêtes statistiques (BIGINT)
-- =========================================================================

-- 1. Table des transactions de trading (Financier)
CREATE TABLE IF NOT EXISTS financial_transactions (
    transaction_id BIGSERIAL PRIMARY KEY, -- Index local ultra-léger
    buyer_id BIGINT NOT NULL,              -- Référence users.id (long)
    seller_id BIGINT NOT NULL,             -- Référence users.id (long)
    item_id BIGINT NOT NULL,              -- Référence catalogue items (long)
    
    gross_amount NUMERIC(12, 2) NOT NULL,
    commission_amount NUMERIC(12, 2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'EUR' NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Indexations critiques pour tes futurs rapports BI (Ventes par Statut, Acheteur, Vendeur, Objet)
CREATE INDEX IF NOT EXISTS idx_transactions_status ON financial_transactions(status);
CREATE INDEX IF NOT EXISTS idx_transactions_buyer ON financial_transactions(buyer_id);
CREATE INDEX IF NOT EXISTS idx_transactions_seller ON financial_transactions(seller_id);
CREATE INDEX IF NOT EXISTS idx_transactions_item ON financial_transactions(item_id);

-- 2. Grand Livre Comptable Immuable (Ledger)
CREATE TABLE IF NOT EXISTS financial_ledger (
    ledger_id BIGSERIAL PRIMARY KEY,
    transaction_id BIGINT REFERENCES financial_transactions(transaction_id) ON DELETE RESTRICT,
    account_type VARCHAR(50) NOT NULL, -- SYSTEM_REVENUE, SELLER_PAYOUT, BUYER_HOLD
    debit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    credit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    description VARCHAR(255) NOT NULL,
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_ledger_recorded_at ON financial_ledger(recorded_at);
CREATE INDEX IF NOT EXISTS idx_ledger_transaction ON financial_ledger(transaction_id);