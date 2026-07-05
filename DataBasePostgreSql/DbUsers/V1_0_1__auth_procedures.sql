-- Inscription alignée sur la colonne business_name
CREATE OR REPLACE FUNCTION api_register_user(
    IN p_business_name VARCHAR(100),
    IN p_email VARCHAR(255),
    IN p_password_hash VARCHAR(500),
    OUT p_user_id BIGINT
)
LANGUAGE plpgsql
AS $$
BEGIN
    -- Force l'email en minuscules pour éviter les doublons masqués
    IF EXISTS (SELECT 1 FROM users WHERE email = LOWER(p_email) AND deleted_at IS NULL) THEN
        RAISE EXCEPTION 'EMAIL_ALREADY_EXISTS';
    END IF;

    -- Ajout de cgu_accepted_at exigé par la contrainte de la table users
    INSERT INTO users (business_name, email, password_hash, is_active, cgu_accepted_at, created_at)
    VALUES (p_business_name, LOWER(p_email), p_password_hash, true, NOW(), NOW())
    RETURNING id INTO p_user_id;
END;
$$;

-- Login
CREATE OR REPLACE FUNCTION api_get_user_for_login(
    IN p_email VARCHAR(255),
    OUT p_id BIGINT,
    OUT p_business_name VARCHAR(100),
    OUT p_password_hash VARCHAR(500),
    OUT p_is_active BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    SELECT id, business_name, password_hash, is_active
    INTO p_id, p_business_name, p_password_hash, p_is_active
    FROM users
    WHERE email = LOWER(p_email) AND deleted_at IS NULL;
END;
$$;