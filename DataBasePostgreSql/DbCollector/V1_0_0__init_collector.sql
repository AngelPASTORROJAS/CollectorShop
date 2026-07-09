-- =========================================================================
-- MIGRATION : V1_0_0__init_collector.sql
-- CIBLE     : collector_db (Bulle Catalogue & Inventaire)
-- SYNCHRO   : Gestion des Objets de Collection + Chargement RAM C#
-- =========================================================================

-- Création des types énumérés exclusifs au domaine Collector
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_type WHERE typname = 'item_status') THEN
        CREATE TYPE item_status AS ENUM ('AVAILABLE', 'RESERVED', 'SOLD', 'ARCHIVED');
    END IF;
END $$;

-- 1. Table des catégories d'objets (ex: Cartes, Pièces, Figurines)
CREATE TABLE IF NOT EXISTS categories (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50) NOT NULL UNIQUE,       -- Code technique pour l'API C# (ex: 'POKEMON_CARD')
    name VARCHAR(100) NOT NULL,             -- Nom affiché (ex: 'Cartes Pokémon')
    is_active BOOLEAN NOT NULL DEFAULT TRUE
);

-- 2. Table principale des objets de collection (Items)
CREATE TABLE IF NOT EXISTS items (
    id BIGSERIAL PRIMARY KEY,
    category_id INT NOT NULL REFERENCES categories(id) ON DELETE RESTRICT,
    owner_id BIGINT NOT NULL,               -- Logique users.id (DbUsers) - Propriétaire actuel
    
    title VARCHAR(150) NOT NULL,
    description TEXT,
    price NUMERIC(12, 2) NOT NULL,          -- Prix de vente proposé
    
    status item_status DEFAULT 'AVAILABLE' NOT NULL,
    
    -- CARACTÉRISTIQUES DYNAMIQUES (Haute performance, stockage binaire indexé)
    metadata JSONB DEFAULT '{}'::jsonb NOT NULL,

    -- TRACEABILITÉ & AUDIT
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    updated_by BIGINT,                      -- ID de l'utilisateur ayant modifié l'objet
    updated_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    deleted_at TIMESTAMP WITH TIME ZONE DEFAULT NULL,
    deleted_by BIGINT                       -- ID de l'utilisateur ayant supprimé l'objet
);

-- Index partiel ultra-performant pour le chargement RAM (Uniquement les objets disponibles à la vente)
CREATE INDEX IF NOT EXISTS idx_items_active_catalog ON items(id) 
WHERE status = 'AVAILABLE' AND deleted_at IS NULL;

-- Index pour accélérer les recherches par propriétaire (Mes Objets en vente)
CREATE INDEX IF NOT EXISTS idx_items_owner ON items(owner_id) WHERE deleted_at IS NULL;

-- Index GIN spécifique sur le JSONB pour des requêtes instantanées à l'intérieur du JSON (ex: filtrer sur PSA 10)
CREATE INDEX IF NOT EXISTS idx_items_metadata_gin ON items USING gin (metadata);


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

-- 2. Application du trigger sur la table items
CREATE OR REPLACE TRIGGER set_timestamp_items
    BEFORE UPDATE ON items
    FOR EACH ROW
    EXECUTE FUNCTION trigger_set_timestamp();


-- =========================================================================
-- Fonctions
-- =========================================================================

-- 1. Chargement à chaud du catalogue actif en RAM (C#)
CREATE OR REPLACE FUNCTION api_load_catalog_ram()
RETURNS TABLE (
    id BIGINT,
    category_code VARCHAR(50),
    owner_id BIGINT,
    title VARCHAR(150),
    price NUMERIC(12, 2),
    metadata_json TEXT 
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        i.id,
        c.code AS category_code,
        i.owner_id,
        i.title,
        i.price,
        i.metadata::text AS metadata_json
    FROM items i
    INNER JOIN categories c ON i.category_id = c.id
    WHERE i.status = 'AVAILABLE' 
      AND i.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;


-- 2. Création d'un nouvel objet
CREATE OR REPLACE FUNCTION api_create_item(
    p_category_code VARCHAR(50),
    p_owner_id BIGINT,
    p_title VARCHAR(150),
    p_description TEXT,
    p_price NUMERIC(12, 2),
    p_metadata_json TEXT 
)
RETURNS BIGINT AS $$
DECLARE
    v_category_id INT;
    v_item_id BIGINT;
BEGIN
    IF p_price <= 0 THEN
        RAISE EXCEPTION 'Le prix de l''objet doit être supérieur à zéro.';
    END IF;

    SELECT id INTO v_category_id FROM categories WHERE code = p_category_code AND is_active = TRUE;
    IF v_category_id IS NULL THEN
        RAISE EXCEPTION 'La catégorie spécifiée % n''existe pas ou est inactive.', p_category_code;
    END IF;

    INSERT INTO items (
        category_id, owner_id, title, description, price, metadata, updated_by
    ) VALUES (
        v_category_id, 
        p_owner_id, 
        TRIM(p_title), 
        TRIM(p_description), 
        p_price, 
        p_metadata_json::jsonb, 
        p_owner_id
    )
    RETURNING id INTO v_item_id;

    RETURN v_item_id;
END;
$$ LANGUAGE plpgsql;


-- 3. Suppression logique (Soft Delete) avec traçabilité de l'auteur
CREATE OR REPLACE FUNCTION api_soft_delete_item(
    p_item_id BIGINT,
    p_deleted_by_id BIGINT)RETURNS BOOLEAN AS $$DECLARE
    v_rows_updated INT;BEGIN
    UPDATE items
    SET 
        status = 'ARCHIVED',
        deleted_by = p_deleted_by_id,
        deleted_at = NOW()
    WHERE id = p_item_id 
      AND deleted_at IS NULL;

    GET DIAGNOSTICS v_rows_updated = ROW_COUNT;
    RETURN v_rows_updated > 0;END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION api_get_item_by_id(p_item_id BIGINT)
RETURNS TABLE (
    id BIGINT,
    category_code VARCHAR(50),
    owner_id BIGINT,
    title VARCHAR(150),
    description TEXT,
    price NUMERIC(12, 2),
    status VARCHAR(50),
    metadata_json TEXT
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        i.id,
        c.code AS category_code,
        i.owner_id,
        i.title,
        i.description,
        i.price,
        i.status::varchar,
        i.metadata::text AS metadata_json
    FROM items i
    INNER JOIN categories c ON i.category_id = c.id
    WHERE i.id = p_item_id 
      AND i.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION api_get_item_owner(p_item_id BIGINT)
RETURNS TABLE (owner_id BIGINT) AS $$
BEGIN
    RETURN QUERY
    SELECT i.owner_id 
    FROM items i
    WHERE i.id = p_item_id AND i.deleted_at IS NULL;
END;
$$ LANGUAGE plpgsql;

-- Données de référence initiales pour vos tests
INSERT INTO categories (code, name) VALUES 
('POKEMON_CARD', 'Cartes Pokémon'),
('ANCIENT_COIN', 'Pièces de Monnaie Anciennes'),
('ACTION_FIGURE', 'Figurines d''Action'),
('CONSOLES', 'Console de jeux video')
ON CONFLICT (code) DO NOTHING;