# Collector Shop API - Prototype Architectural V1 (Lead Dev)

Ce dépôt contient le prototype (POC) hautement sécurisé et optimisé d'une plateforme d'échange pour collectionneurs. L'application implémente une architecture modulaire multi-tenancy logicielle s'appuyant sur l'isolement des contextes de données.

## 🛠️ Stack Technique & Architecture
* **Backend :** .NET 10 Web API (C#) sans mapping mental (Isomorphe JSON).
* **Bases de données :** Multi-PostgreSQL 17 (3 instances isolées : `Identité/RGPD`, `Catalogue/Métier`, `Haute Sécurité/Finance`).
* **Performance :** Cache global centralisé en RAM et compression applicative Gzip (Fastest).
* **Sécurité & Observabilité :** HTTPS Forcé, HSTS, Middlewares d'Authentification Custom par Cookie/Session, et HealthChecks de cluster intégrés.

---

## 📐 Alignement Exigences Qualité (ISO 25010) & Métriques

Pour valider ce bloc, le processus de développement s'appuie sur 4 indicateurs clés intégrés au cycle CI/CD afin de bloquer l'accumulation de dette technique :

| Indicateur / Métrique | Exigence ISO 25010 | Outil de Mesure | Justification Anti-Dette |
| :--- | :--- | :--- | :--- |
| **Taux de couverture de scénarios** | Fiabilité & Pertinence | `api.tests` (Dockerized xUnit) | Garantit le non-effondrement des règles métier complexes multi-bases lors des refactoring. |
| **Densité de vulnérabilités (CVE)** | Sécurité | Scan SAST / Trufflehog | Bloque en amont l'introduction de packages obsolètes ou de fuites de secrets dans le code. |
| **Temps de réponse de la base (ms)** | Efficacité des performances | Logs d'observabilité ADO.NET | Permet de détecter immédiatement les régressions d'indexation ou les requêtes SQL bloquantes. |
| **Disponibilité des dépendances** | Robustesse / Disponibilité | Endpoint `/health` (HealthChecks) | Empêche la mise en production d'une V1 si l'une des 3 bulles PostgreSQL est instable. |

---

## 🔄 Pipeline DevSecOps (CI/CD)

Le cycle de vie de livraison est entièrement automatisé via GitHub Actions (`.github/workflows/ci-cd.yml`) et structuré ainsi :
1. **Étape SecOps :** Analyse statique du code et scan de secrets (Trufflehog).
2. **Étape Build :** Restauration et compilation stricte sous .NET 10.
3. **Étape Intégration (POC Évalué) :** Orchestration par Docker Compose. Lancement automatique de `database.migrator` (migrations SQL brutes), vérification de la santé des conteneurs (`healthcheck`), instanciation de l'API et exécution du runner de scénarios complexes (`api.tests`). Le pipeline échoue si un test échoue ou si une DB est instable.

---

## 🚀 Lancement Local Rapide

### Prérequis
* Docker et Docker Compose installés.
* SDK .NET 10 (optionnel pour l'exécution locale isolée).

### Exécuter le prototype et sa suite de tests complète
```bash
# Clone du dépôt
git clone https://github.com/AngelPASTORROJAS/CollectorShop.git
cd CollectorShop

# Lancement de l'infrastructure complète et validation par tests d'intégration
docker-compose up --build
