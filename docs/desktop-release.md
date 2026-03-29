# Desktop release

Cette app desktop peut maintenant etre publiee comme une vraie application Windows installable, avec mises a jour automatiques.

## Ce qui est en place

- L'installeur desktop embarque maintenant un backend publie dans un dossier `backend/`.
- L'app desktop sait verifier un feed de release Velopack au demarrage et proposer un redemarrage quand une mise a jour est prete.
- Le jeton desktop stocke localement est protege via DPAPI Windows dans `%LocalAppData%\LolLiveCoach\overlay-settings.json`.
- Le workflow GitHub Actions `desktop-release` peut publier le feed de mise a jour sur GitHub Pages.

## Premiere mise en service

1. Active GitHub Pages sur le repo en choisissant `GitHub Actions` comme source de deploiement.
2. Cree la variable de repository `DESKTOP_PLATFORM_BASE_URL` avec l'URL publique du site web qui gere les comptes desktop.
3. Si tu veux eviter les alertes SmartScreen, prepare un certificat de signature de code et renseigne `VELOPACK_SIGN_TEMPLATE` en secret GitHub.

## Build local

```powershell
pwsh ./scripts/publish-desktop.ps1 `
  -Version 0.1.0 `
  -Channel stable `
  -UpdateFeedUrl https://polyaga.github.io/lol-live-coach/desktop/stable `
  -PlatformBaseUrl https://ton-site-public.example
```

Le dossier genere est `artifacts/desktop/release/win-x64/stable`.

## Release GitHub

Deux options :

1. Creer un tag `desktop-v0.1.0` puis le pousser.
2. Lancer le workflow `desktop-release` manuellement avec la version voulue.

Le workflow :

- compile le desktop et le backend en `Release`
- genere l'installeur et les packages Velopack
- publie le feed de mise a jour sur GitHub Pages
- garde aussi le build en artifact GitHub Actions

## Distribution testeur

Envoie l'installeur genere dans le feed publie sur Pages. Le fichier exact est produit dans `artifacts/desktop/release/win-x64/stable`.

Le nom actuel de l'installeur est :

```text
Polyaga.LolLiveCoach-stable-Setup.exe
```

Le feed de mise a jour vit a cette URL :

```text
https://polyaga.github.io/lol-live-coach/desktop/stable
```

Une fois la premiere version installee depuis ce canal, les suivantes seront detectees automatiquement par l'app.

## Checklist securite avant vrai prod

- Utiliser un vrai domaine HTTPS pour `DESKTOP_PLATFORM_BASE_URL`.
- Signer les executables Windows pour reduire SmartScreen et verifier l'integrite.
- Garder les secrets web hors git et ne jamais versionner `.env.local`.
- Remplacer toutes les cles de test Stripe et mots de passe d'admin par des secrets de prod.
- Pour un trafic plus large qu'un simple test, preferer un feed S3 / Azure Blob / GitHub Releases plutot que GitHub Pages.
