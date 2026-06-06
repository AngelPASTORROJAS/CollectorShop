-- =========================================================================
-- MIGRATION : V1_0_0__init_users_and_access.sql
-- CIBLE     : users_db (Bulle Identité)
-- SYNCHRO   : Support Multi-Tenant via user_group et droits délégués
-- =========================================================================

CREATE TABLE IF NOT EXISTS users (
    id BIGSERIAL PRIMARY KEY,
    email VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    user_group VARCHAR(50), -- Contient le nom/ID de l'entreprise du client
    
    -- NOM COMMERCIAL (Non unique, autorise les doublons)
    business_name VARCHAR(100) NOT NULL, 

    -- AJOUTS CONFORMITÉ RGPD & AUDIT TRAIL
    cgu_accepted_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_by BIGINT, -- ID de l'utilisateur ou du système ayant fait la modification
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_by BIGINT, -- ID de l'admin ou du manager ayant supprimé le compte (Rejeter la faute)
    deleted_at TIMESTAMP WITH TIME ZONE DEFAULT NULL
);

-- Index partiel pour maximiser les performances de sp_load_users_ram
CREATE INDEX IF NOT EXISTS idx_users_active_partial ON users(id) WHERE deleted_at IS NULL;
CREATE INDEX IF NOT EXISTS idx_users_user_group ON users(user_group);

CREATE TABLE IF NOT EXISTS access (
    id SERIAL PRIMARY KEY,
    code VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(250) NOT NULL
);

CREATE TABLE IF NOT EXISTS user_access (
    user_id BIGINT NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    access_id INT NOT NULL REFERENCES access(id) ON DELETE CASCADE,
    PRIMARY KEY (user_id, access_id)
);

-- Insertion des droits de base étendus pour la gestion déléguée
INSERT INTO access (code, description) VALUES 
('CONFIG_RELOAD', 'Accès au rechargement du cache à chaud'),
('USER_MANAGE_ALL', 'Gestion globale de TOUS les utilisateurs (Super Admin)'),
('USER_MANAGE_GROUP', 'Gestion des utilisateurs de son PROPRE groupe uniquement (Admin Client)')
ON CONFLICT (code) DO NOTHING;

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

-- 2. Application du trigger sur la table users
CREATE OR REPLACE TRIGGER set_timestamp_users
    BEFORE UPDATE ON users
    FOR EACH ROW
    EXECUTE FUNCTION trigger_set_timestamp();

-- =========================================================================
-- MIGRATION : Procédures stockées
-- =========================================================================

CREATE OR REPLACE FUNCTION sp_create_user(
    p_email VARCHAR(150),
    p_password_hash VARCHAR(255),
    p_business_name VARCHAR(100),
    p_user_group VARCHAR(50) DEFAULT NULL,
    p_access_code VARCHAR(50) DEFAULT NULL -- Permet d'injecter un droit de base à la création (ex: 'USER_MANAGE_GROUP')
)
RETURNS BIGINT AS $$
DECLARE
    v_user_id BIGINT;
    v_access_id INT;
BEGIN
    -- 1. Vérification de sécurité applicative (Évite de polluer les logs avec des crashs de contrainte)
    IF EXISTS (SELECT 1 FROM users WHERE email = p_email AND deleted_at IS NULL) THEN
        RAISE EXCEPTION 'L''adresse email % est déjà associée à un compte actif.', p_email;
    END IF;

    -- 2. Insertion de l'utilisateur avec validation forcée des CGU (Horodatage synchrone)
    INSERT INTO users (
        email, 
        password_hash, 
        business_name, 
        user_group, 
        cgu_accepted_at
    ) VALUES (
        LOWER(TRIM(p_email)), -- Normalisation pour éviter les contournements par casse (ex: Test@Test.com)
        p_password_hash, 
        p_business_name, 
        p_user_group, 
        NOW() -- L'acceptation des CGU est datée immédiatement à l'inscription
    )
    RETURNING id INTO v_user_id;

    -- 3. Attribution optionnelle d'un droit d'accès initial (ex: Rôles de gestion)
    IF p_access_code IS NOT NULL THEN
        -- On récupère l'identifiant interne du privilège
        SELECT id INTO v_access_id FROM access WHERE code = p_access_code;
        
        -- Si le code d'accès existe, on effectue l'association dans la table de liaison
        IF v_access_id IS NOT NULL THEN
            INSERT INTO user_access (user_id, access_id) 
            VALUES (v_user_id, v_access_id);
        ELSE
            RAISE NOTICE 'Le code d''accès % n''existe pas. L''utilisateur a été créé sans privilèges.', p_access_code;
        END IF;
    END IF;

    -- Renvoie l'ID pour permettre à l'API de construire sa réponse (ex: CreatedAtAction / HTTP 201)
    RETURN v_user_id;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_load_users_ram()
RETURNS TABLE (
    id BIGINT,
    business_name VARCHAR(100),
    email VARCHAR(150),
    is_active BOOLEAN,
    user_group VARCHAR(50),
    can_config_reload BOOLEAN,
    can_user_manage_all BOOLEAN,
    can_user_manage_group BOOLEAN) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        u.id,
        u.business_name,
        u.email,
        u.is_active,
        u.user_group,
        COALESCE(MAX(CASE WHEN a.code = 'CONFIG_RELOAD' THEN 1 END), 0) = 1 AS can_config_reload,
        COALESCE(MAX(CASE WHEN a.code = 'USER_MANAGE_ALL' THEN 1 END), 0) = 1 AS can_user_manage_all,
        COALESCE(MAX(CASE WHEN a.code = 'USER_MANAGE_GROUP' THEN 1 END), 0) = 1 AS can_user_manage_group
    FROM users u
    LEFT JOIN user_access ua ON u.id = ua.user_id
    LEFT JOIN access a ON ua.access_id = a.id
    WHERE u.deleted_at IS NULL
    GROUP BY u.id, u.business_name, u.email, u.is_active, u.user_group;
END;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION sp_soft_delete_user(
    p_user_id BIGINT,
    p_deleted_by_id BIGINT
)
RETURNS BOOLEAN AS $$
DECLARE
    v_rows_updated INT;
BEGIN
    -- Mises à jour avec anonymisation des données sensibles (Conformité RGPD)
    UPDATE users
    SET 
        -- On préserve explicitement 'business_name' pour la BI et la traçabilité à long terme
        email = 'deleted_' || p_user_id || '@collector-shop.internal',
        password_hash = 'GDPR_ANONYMIZED',
        is_active = FALSE,
        user_group = NULL,
        deleted_by = p_deleted_by_id,
        deleted_at = NOW()              -- Le trigger 'set_timestamp_users' mettra aussi à jour 'updated_at'
    WHERE id = p_user_id 
      AND deleted_at IS NULL;

    GET DIAGNOSTICS v_rows_updated = ROW_COUNT;

    -- Supprime également tous ses accès spécifiques par sécurité
    IF v_rows_updated > 0 THEN
        DELETE FROM user_access WHERE user_id = p_user_id;
        RETURN TRUE;
    END IF;

    RETURN FALSE;
END;
$$ LANGUAGE plpgsql;
