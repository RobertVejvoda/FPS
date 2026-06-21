# FairSpot Keycloak Login Theme

## Location

```
code/infrastructure/keycloak/themes/fairspot/
└── login/
    ├── theme.properties             # parent=keycloak; styles override
    ├── login.ftl                    # main login page
    ├── login-reset-password.ftl     # forgot-password page
    ├── login-update-password.ftl    # post-reset set-new-password page
    ├── error.ftl                    # generic error page
    ├── messages/
    │   └── messages_en.properties   # business-friendly English overrides
    └── resources/
        └── css/
            └── login.css            # FairSpot brand CSS (no external deps)
```

## What It Looks Like

**Normal login** — centered card on a light green-tinted background; FairSpot logo mark + wordmark; email/password fields; "Sign in" primary button; optional "Forgot password?" link.

**Error state** — same card with a red alert banner above the form for credential errors; field-level inline error text for individual field failures.

**Forgot password / Update password** — same branded shell; instructional copy; single field; back-to-login link.

**Generic error page** — warning icon + error summary; "Back to application" link when a client base URL is configured.

## Enabling the Theme

### Local development (Docker Compose)

The theme is mounted automatically via `docker-compose.yaml`:

```yaml
volumes:
  - ./keycloak/themes:/opt/keycloak/themes:ro
```

`fps-local-realm.json` already sets `"loginTheme": "fairspot"`, so importing the realm is all that is needed.

To import the realm on a fresh Keycloak instance:

1. Start Keycloak: `docker compose up keycloak -d`
2. Open http://localhost:8180 → Admin Console → admin / admin
3. Create realm → Import → select `keycloak/fps-local-realm.json`

The FairSpot login theme will be active immediately at:
`http://localhost:8180/realms/fps-local/account/`

### NAS / customer-demo deployment

1. Mount the `themes/fairspot` directory into `/opt/keycloak/themes/fairspot` on the Keycloak container (bind mount or Docker volume).
2. In the Keycloak Admin Console, navigate to **Realm settings → Themes → Login theme** and select `fairspot`.
3. Click **Save**. The theme takes effect immediately — no restart required in `start-dev` mode; a Keycloak restart may be needed in production (`start`) mode.

Alternatively, set `loginTheme` in the realm export JSON and re-import:

```json
{
  "realm": "fps-local",
  "loginTheme": "fairspot"
}
```

### Reverting to the default theme

In the Keycloak Admin Console: **Realm settings → Themes → Login theme → (select blank/keycloak) → Save**.

The default Keycloak theme remains fully operational when `fairspot` is not selected — no source changes are required.

## Customisation

| What to change | Where |
|---|---|
| Brand colours / typography | `resources/css/login.css` (CSS custom properties at top of file) |
| Button / link text | `messages/messages_en.properties` |
| Page layout / form fields | `login.ftl`, `login-reset-password.ftl`, etc. |
| Logo / image assets | Add files to `resources/img/` and reference in `login.ftl` |

## Accessibility Notes

- Colour contrast: primary blue (#2563eb) on white ≥ 4.5:1 (WCAG AA).
- Focus indicators: 3px offset focus ring on all interactive elements.
- Keyboard flow: `tabindex` ordering on all form fields.
- Error messages linked to inputs via `aria-describedby`; live regions on field errors.
- `prefers-reduced-motion` media query disables transitions for motion-sensitive users.

## Validation Notes

Runtime Keycloak was not started during this PR because the NAS/production Keycloak instance is not available locally without the full Docker stack. To verify:

1. Run `docker compose up keycloak -d` from `code/infrastructure/`
2. Import `fps-local-realm.json` into Keycloak admin
3. Open `http://localhost:8180/realms/fps-local/protocol/openid-connect/auth?client_id=fps-web&response_type=code&redirect_uri=http://localhost:5000`
4. Confirm FairSpot branding appears (logo mark, green-tinted background, blue "Sign in" button)
5. Enter wrong credentials → confirm inline error message appears
6. Click "Forgot password?" → confirm reset page renders with FairSpot branding
