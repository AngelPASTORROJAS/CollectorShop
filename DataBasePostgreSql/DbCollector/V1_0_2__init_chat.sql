-- =========================================================================
-- MIGRATION : V1_0_2__init_chat.sql
-- CIBLE     : collector_db (Bulle Catalogue & Inventaire)
-- SYNCHRO   : Gestion de la messagerie instantanée (Chat) entre acheteurs et vendeurs
-- =========================================================================

-- 1. Table des messages
CREATE TABLE IF NOT EXISTS messages (
    id BIGSERIAL PRIMARY KEY,
    item_id BIGINT NOT NULL REFERENCES items(id) ON DELETE CASCADE,
    sender_id BIGINT NOT NULL,
    receiver_id BIGINT NOT NULL,
    content TEXT NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL
);

-- Index pour accélérer la récupération de l'historique d'un chat pour un objet donné
CREATE INDEX IF NOT EXISTS idx_messages_item ON messages(item_id, created_at);

-- =========================================================================
-- Fonctions
-- =========================================================================

-- 1. Récupérer l'historique des messages pour un article
CREATE OR REPLACE FUNCTION api_get_messages_for_item(
    p_item_id BIGINT
)
RETURNS TABLE (
    id BIGINT,
    item_id BIGINT,
    sender_id BIGINT,
    receiver_id BIGINT,
    content TEXT,
    created_at TIMESTAMP WITH TIME ZONE
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        m.id,
        m.item_id,
        m.sender_id,
        m.receiver_id,
        m.content,
        m.created_at
    FROM messages m
    WHERE m.item_id = p_item_id
    ORDER BY m.created_at ASC;
END;
$$ LANGUAGE plpgsql;

-- 2. Envoyer un nouveau message
CREATE OR REPLACE FUNCTION api_send_message(
    p_item_id BIGINT,
    p_sender_id BIGINT,
    p_receiver_id BIGINT,
    p_content TEXT
)
RETURNS BIGINT AS $$
DECLARE
    v_message_id BIGINT;
BEGIN
    IF TRIM(p_content) = '' THEN
        RAISE EXCEPTION 'Le contenu du message ne peut pas être vide.';
    END IF;

    INSERT INTO messages (
        item_id, sender_id, receiver_id, content
    ) VALUES (
        p_item_id, p_sender_id, p_receiver_id, TRIM(p_content)
    )
    RETURNING id INTO v_message_id;

    RETURN v_message_id;
END;
$$ LANGUAGE plpgsql;
