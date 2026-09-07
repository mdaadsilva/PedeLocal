# PedeLocal · Painel do estabelecimento

Next.js (App Router) em `apps/merchant-web`. Consome a API existente via BFF
(`/api/backend` + cookies httpOnly). Não altera o backend.

```bash
cp .env.example .env.local
npm install
npm run dev
```

Login de desenvolvimento: `loja@pedelocal.dev` / `Senha123!`

Validações:

```bash
npm run lint
npm run test
npm run build
```
