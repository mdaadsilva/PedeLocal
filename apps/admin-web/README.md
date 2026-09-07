# PedeLocal · Painel Super Admin

Next.js (App Router) em `apps/admin-web`. Consome a API existente via BFF
(`/api/backend` + cookies httpOnly). Não altera o backend.

```bash
cp .env.example .env.local
npm install
npm run dev
```

O painel sobe em `http://localhost:3001`.

Login de desenvolvimento: `admin@pedelocal.dev` / `Senha123!`

Somente o papel `SUPER_ADMIN` entra. Outros papéis recebem 403 no BFF e os
tokens são revogados.

Validações:

```bash
npm run lint
npm run test
npm run build
```

Gaps do backend: `docs/ADMIN_BACKEND_GAPS.md`.
