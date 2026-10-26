# SafeNet — Cybersecurity Awareness System

SafeNet is a capstone project that teaches college students about real-world cyber threats through a gamified 2D mobile game and a faculty web portal. Students play through six graded missions covering phishing, smishing, social engineering, password security, data privacy, and web safety, while professors monitor class performance through a dedicated dashboard.

## Project Structure

```
SafeNet-Portfolio/
├── safenet-web/          # MERN stack web portal
│   ├── server/           # Express + MongoDB API
│   │   ├── src/
│   │   │   ├── config/       # Database connection, email, progress settings
│   │   │   ├── controllers/  # Route handlers for auth, classes, progress, dashboard
│   │   │   ├── middleware/    # JWT auth, rate limiting, request security, errors
│   │   │   ├── models/       # Mongoose schemas (professor, student, class, missions)
│   │   │   ├── routes/       # API endpoint definitions
│   │   │   ├── services/     # Email delivery, password reset, verification
│   │   │   └── utils/        # JWT token helpers, student payload formatting
│   │   ├── package.json
│   │   └── .env.example
│   └── client/           # React + Vite frontend
│       ├── src/
│       │   ├── api/          # API client with cookie-based auth
│       │   ├── components/   # Reusable UI (password input, weekday picker, etc.)
│       │   ├── hooks/        # Custom React hooks
│       │   ├── pages/        # Main views (home, registration, portal, verification)
│       │   ├── styles/       # CSS stylesheets
│       │   └── utils/        # Registration state helpers
│       ├── package.json
│       ├── vite.config.js
│       ├── vercel.json
│       └── .env.example
│
└── safenet-game/         # Unity 2D Android game (C# scripts)
    └── Scripts/
        ├── Core/             # GameManager, ApiClient, Constants, Application
        ├── Audio/            # Music manager, SFX player, voice lines
        ├── Input/            # Mobile touch + desktop keyboard input
        ├── UI/               # Dialogue controller, HUD, responsive canvas, themes
        ├── Gameplay/
        │   ├── Dialogue/     # Branching dialogue data and runner
        │   ├── Interaction/  # IInteractable contract for NPCs and objects
        │   ├── Player/       # Player movement controller
        │   ├── Progression/  # Shared mission-exit behavior
        │   └── Visuals/      # Background animations and scene previews
        └── Scenes/
            ├── Menus/        # Login, start menu, mission selection, cinematic
            ├── Tutorial/     # Tutorial flow and interactions
            ├── Missions/     # Area01-06 (one folder per mission)
            └── Results/      # Game complete and game over screens
```

## Tech Stack

### Web Portal (MERN)
| Layer | Technology |
|-------|-----------|
| Frontend | React 19, Vite 8 |
| Backend | Express 5, Node.js 22+ |
| Database | MongoDB Atlas (Mongoose 9) |
| Auth | bcrypt password hashing, JWT access/refresh tokens (httpOnly cookies) |
| Email | Nodemailer via Gmail OAuth2 API |
| Validation | Zod schema validation |
| Security | Helmet, CORS, express-rate-limit, request sanitization |
| Deployment | Vercel (client) + Render/Vercel (server) |

### Mobile Game (Unity)
| Layer | Technology |
|-------|-----------|
| Engine | Unity 2D (C#) |
| Platform | Android (APK) |
| Architecture | Singleton services (GameManager, ApiClient, AudioManager) |
| Dialogue | Data-driven branching system with score modifiers |
| Input | Dual-mode: mobile touch controls + desktop keyboard |
| Backend Sync | REST API client for score/progress upload |

## Features

### For Students (Mobile Game)
- **6 graded missions** covering phishing, smishing, social engineering, password security, data privacy, and web safety
- **Tutorial mission** teaching controls before graded play
- **Branching dialogue** with choices that affect score (correct answers earn points, wrong answers damage system integrity)
- **3-strike system integrity** — lose all 3 and it's game over
- **Sequential mission unlocking** — complete one to unlock the next
- **Performance bands** based on total score:
  - Cyber Defender (45+)
  - Security Specialist (35+)
  - Threat Analyst (25+)
  - Awareness Trainee (<25)
- **Score sync** with the web portal for professor review
- **Voice lines and music** per mission area

### For Professors (Web Portal)
- **Professor registration** with email verification
- **Class management** — create classes with join codes for students
- **Student dashboard** — view per-student mission progress, scores, and completion status
- **Game run tracking** — detailed logs of each student's play sessions
- **Password reset** via email

## API Routes

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/api/auth/professors/register` | Professor registration |
| POST | `/api/auth/professors/login` | Professor login (JWT cookies) |
| POST | `/api/auth/students/login` | Student login from Unity game |
| POST | `/api/auth/students/register` | Student self-registration |
| GET | `/api/professor/dashboard` | Class analytics and student progress |
| POST | `/api/students/progress/mission` | Save mission score from game |
| GET | `/api/students/progress/profile` | Fetch student profile and scores |
| POST | `/api/classes` | Create a new class |
| GET | `/api/classes/verify/:code` | Verify a class join code |
| POST | `/api/email-verification/verify` | Verify email address |
| GET | `/api/health` | Server health check |

## Mission Map

| Mission | Area | Cyber Threat | Gameplay |
|---------|------|-------------|----------|
| 1 | Phishing | Email phishing | NPC dialogue with suspicious email analysis |
| 2 | Smishing | SMS phishing | Two scam message scenarios + message routing minigame |
| 3 | Social Engineering | Identity manipulation | Urgent account alert, verification code theft, impersonation |
| 4 | Password Security | Weak credentials | Weak/strong password terminals, password reuse, credential sharing |
| 5 | Data Privacy | Personal data exposure | Review personal info before sharing decision |
| 6 | Web Safety | Malicious websites | URL verification, malware pop-ups, source reliability, download traps |



## Security Highlights

- All secrets are loaded from environment variables (never hardcoded)
- JWT tokens stored in httpOnly, secure, SameSite cookies
- Password hashing with bcrypt (cost factor 12)
- Rate limiting on all API routes (separate limits for auth vs. general)
- Helmet security headers (CSP, HSTS, X-Frame-Options, etc.)
- Zod input validation on all request bodies
- Request body size limits (16KB)
- MongoDB connection security with TLS and server selection timeout

## License

This project was developed as a capstone project for academic purposes.
