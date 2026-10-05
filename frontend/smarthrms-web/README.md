# SmartHRMS Web

React + TypeScript frontend for the SmartHRMS API (Day 1–18): sign-in with roles, dashboard, employees (with photo),
departments, designations, employee profiles, documents, attendance, leave (apply / approve), salary structures,
payroll (periods, calculation, review, approval, finalization), printable payslips, payroll history and salary
payments (payment batches, payment processing, payment history, the employee's own payments).

## Sign-in and roles

The API issues a JWT at `POST /api/auth/login`; the app keeps it in `sessionStorage` (cleared when the tab closes) and
sends it on every request. Any `401` (expired token, account deactivated, role or password changed) returns to the
sign-in page. Roles decide what the sidebar shows and which routes open (`RoleRoute` in `src/routes`); the API checks
every request again, so hiding a page is never the only protection.

| Role | Sees |
|---|---|
| Employee | own dashboard, profile (read-only), attendance (check in/out), leave, payroll, payslips and own payments |
| Manager | the same, plus leave approvals for direct reports |
| HR | everything except users and payroll approval/finalization; payment batches and payment history read-only |
| Admin | everything, including Users & roles, payroll approval and finalization, and payments (create batch, process, paid/failed/retry/cancel; never their own salary) |

First sign-in: the backend creates the first Admin from `Auth:BootstrapAdmin` (see the backend documentation §17).
That Admin then creates accounts on the *Users & roles* page.

Stack: React 19, Vite 8, TypeScript 6, React Router 7, Axios, Tailwind CSS 4 + DaisyUI 5, React Hook Form + Zod, Lucide.

## Run

1. Start the API: `dotnet run --project backend/smartHRMS.Api` (listens on http://localhost:5099).
2. In this folder: `npm install` (first time), then `npm run dev` and open http://localhost:5173.

In development the app calls the API directly at `VITE_API_BASE_URL` (`.env.development`: http://localhost:5099).
The API allows this through its CORS policy (`Cors:AllowedOrigins` in the backend's `appsettings.Development.json`,
which lists http://localhost:5173 and http://localhost:4173). If you run Vite on another port, add that origin there.

Leaving `VITE_API_BASE_URL` empty switches to the fallback: Vite proxies `/api` and `/uploads` to
`VITE_API_PROXY_TARGET`. See `.env.example`.

## Scripts

- `npm run dev`: dev server with the API proxy
- `npm run build`: type-check and build to `dist/`
- `npm run lint`: ESLint
- `npm run preview`: serve the build (same proxy)

## Production

`npm run build` uses production mode, so `.env.development` is not applied: the build calls its own origin.
Either serve `dist/` from the same origin as the API, or build with `VITE_API_BASE_URL` set to the API origin
(e.g. in `.env.production`) and add the frontend's origin to the API's `Cors:AllowedOrigins`.
Any unknown path must fall back to `index.html` (client-side routing).

## Not available yet (backend limitations)

- **PDF payslips.** The payslip page is print-ready (browser *Print → Save as PDF*); there is no server-side PDF.
- **Unit tests.** There is no Vitest/Jest setup; the frontend is verified with browser end-to-end runs.
- **Server-side search, filtering and paging.** The list endpoints return every row, so the employee list filters, sorts and pages in the browser.
