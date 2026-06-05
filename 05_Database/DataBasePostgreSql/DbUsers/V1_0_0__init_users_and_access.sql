-- =========================================================================
-- MIGRATION : V1_0_0__init_users_and_access.sql
-- CIBLE     : users_db (Bulle Identité)
-- SYNCHRO   : Support Multi-Tenant via user_group et droits délégués
-- =========================================================================

CREATE TABLE IF NOT EXISTS users (
    id BIGSERIAL PRIMARY KEY,
    username VARCHAR(50) NOT NULL UNIQUE,
    email VARCHAR(150) NOT NULL UNIQUE,
    password_hash VARCHAR(255) NOT NULL,
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    user_group VARCHAR(50), -- Contient le nom/ID de l'entreprise du client

    -- AJOUTS CONFORMITÉ RGPD
    cgu_accepted_at TIMESTAMP WITH TIME ZONE NOT NULL,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT CURRENT_TIMESTAMP,
    deleted_at TIMESTAMP WITH TIME ZONE DEFAULT NULL
);

CREATE INDEX IF NOT EXISTS idx_users_deleted_at ON users(deleted_at);
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
-- MIGRATION : Procédures stockées
-- =========================================================================

CREATE OR REPLACE FUNCTION sp_load_users_ram()
RETURNS TABLE (
    id BIGINT,
    username VARCHAR(50),
    email VARCHAR(150),
    is_active BOOLEAN,
    user_group VARCHAR(50),
    can_config_reload BOOLEAN,
    can_user_manage_all BOOLEAN,
    can_user_manage_group BOOLEAN
) AS $$
BEGIN
    RETURN QUERY
    SELECT 
        u.id,
        u.username,
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
    GROUP BY u.id, u.username, u.email, u.is_active, u.user_group;
END;
$$ LANGUAGE plpgsql;
