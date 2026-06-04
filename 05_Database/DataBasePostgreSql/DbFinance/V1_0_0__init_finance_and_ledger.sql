-- =========================================================================
-- MIGRATION : V1_0_0__init_finance_and_ledger.sql
-- CIBLE     : finance_db (Bulle Haute Sécurité)
-- =========================================================================

-- 1. Table des transactions de trading (Financier)
CREATE TABLE IF NOT EXISTS financial_transactions (
    transaction_id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    buyer_id UUID NOT NULL,  -- Référence logique vers app_users.user_id (cross-db)
    seller_id UUID NOT NULL, -- Référence logique vers app_users.user_id (cross-db)
    item_id UUID NOT NULL,   -- Référence logique vers le catalogue de l'API Collector
    gross_amount NUMERIC(12, 2) NOT NULL, -- Prix total payé
    commission_amount NUMERIC(12, 2) NOT NULL, -- La commission de 5% prélevée
    currency VARCHAR(3) DEFAULT 'EUR' NOT NULL,
    status VARCHAR(50) DEFAULT 'PENDING' NOT NULL, -- PENDING, COMPLETED, REFUNDED
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_transactions_status ON financial_transactions(status);

-- 2. Grand Livre Comptable Immuable (Ledger - SecOps Audit Trail)
-- Cette table ne doit recevoir que des INSERTS, jamais d'UPDATES ni de DELETES.
CREATE TABLE IF NOT EXISTS financial_ledger (
    ledger_id BIGSERIAL PRIMARY KEY,
    transaction_id UUID REFERENCES financial_transactions(transaction_id),
    account_type VARCHAR(50) NOT NULL, -- SYSTEM_REVENUE, SELLER_PAYOUT, BUYer_HOLD
    debit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    credit_amount NUMERIC(12, 2) DEFAULT 0.00 NOT NULL,
    description VARCHAR(255) NOT NULL,
    recorded_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

CREATE INDEX IF NOT EXISTS idx_ledger_recorded_at ON financial_ledger(recorded_at);
