# LolLiveCoach.Web

Plateforme web Next.js separee de l'application desktop.

## Demarrage

1. Installer les dependances avec `npm install`
2. Copier `.env.example` vers `.env.local`
3. Renseigner `DATABASE_URL`, les variables Stripe et l'URL publique
4. Generer Prisma avec `npm run prisma:generate`
5. Appliquer les migrations avec `npm run prisma:migrate`
6. Lancer `npm run dev`

## Variables importantes

- `STRIPE_SECRET_KEY`: cle secrete Stripe
- `STRIPE_WEBHOOK_SECRET`: secret du webhook Stripe
- `STRIPE_PRICE_MONTHLY`: price id Stripe du plan mensuel
- `STRIPE_PRICE_YEARLY`: price id Stripe du plan annuel
- `SITE_URL`: URL publique utilisee pour les retours Checkout
- `DATABASE_URL`: connexion PostgreSQL pour les comptes, sessions et abonnements
- `DESKTOP_DOWNLOAD_URL`: URL publique de l'installeur desktop genere depuis le feed `desktop/stable`
- `NEXT_PUBLIC_SUPPORT_EMAIL`: email de contact affiche sur le site
- `ADMIN_EMAIL`: email de connexion admin
- `ADMIN_PASSWORD`: mot de passe de l'espace admin
- `ADMIN_SESSION_SECRET`: secret de signature du cookie admin

## Programme ambassadeur

- Page publique sur `/ambassadeur`
- Formulaire de candidature stocke localement dans `data/ambassadors.json`
- Admin simple sur `/admin`
- Validation manuelle, acces offert et suivi du revenu d'affiliation
