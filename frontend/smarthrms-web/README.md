# SmartHRMS Web

React + TypeScript frontend for the SmartHRMS API (Day 1–11): dashboard, employees (with photo), departments,
designations, and employee profiles (personal details, addresses, emergency contacts, education, experience, documents).

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

- **Sign-in.** The API has no authentication, so `/login` explains this, and *My Profile* asks which employee to show.
- **Server-side search, filtering and paging.** The list endpoints return every row, so the employee list filters, sorts and pages in the browser.
