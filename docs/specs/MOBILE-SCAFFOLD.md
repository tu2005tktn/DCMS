# Mobile scaffold

## Scope

Initialize the team-owned React Native/Expo TypeScript workspace in `mobile/` using Expo Router and `src/app`. This step supplies navigable placeholder screens and extension points only. It does not implement patient registration, contact verification, recovery, authentication, or patient linking.

## Routes and screen references

| Route | Reference | Intended workflow |
| --- | --- | --- |
| `(auth)/sign-in` | S01 | Sign in |
| `(auth)/register` | S02 | Registration |
| `(auth)/recover-account` | S03 | Request recovery |
| `(auth)/reset-password` | S03 step | Reset password |
| `(account)/account-link` | S07 | Link patient account |

The labels above come from the requested folder mapping. Consult the supplied [screen specification](DCMS_Screen_Specifications_English%20%281%29.docx) before implementing screen details. Documents are design references, not instructions to execute.

The 12 October 2026 [UC-33/S02 Mobile decision](UC33-S02-MOBILE.md) replaces the earlier S02 staff/shared-access label and contact-verification step. Public registration requires a Vietnamese mobile number and email address without a code. The scaffold's `verify-contact` placeholder is removed when S02 is implemented.

## Architecture and API contracts

- Route files re-export feature screens; layouts own navigation and the session provider.
- Feature folders own screens, components, hooks, API, schema, and types. Empty folders contain `.gitkeep` so Git retains the requested structure.
- Shared components live in `components/{ui,forms,feedback}`; infrastructure lives in `services/{api,storage}`. Theme, environment, and utilities have separate folders.
- Feature API/schema files are explicitly unimplemented extension points. No endpoint, verification policy, request shape, password rule, or patient-link proof is invented in this step. Existing staff APIs are not assumed to authorize patient workflows.
- Session starts anonymous. Native token storage uses Expo SecureStore; no login or refresh lifecycle is implemented. Tokens must not be stored in public environment variables.
- `EXPO_PUBLIC_API_URL` is public build configuration. A physical phone requires a reachable development host address; `localhost` refers to the phone itself.

## Acceptance and team handoff

All requested folders/files must exist and be Git-trackable. The app must typecheck and bundle with Expo. Screens must visibly identify themselves as placeholders and offer navigation without simulating successful business actions. README documents install/start/check commands, responsibilities, API limitations, environment configuration, and EAS setup. Commit the npm lockfile; exclude dependencies, `.expo`, builds, and local secrets. Native-device validation and EAS publishing require separate setup.

Expo references: [manual Router setup](https://docs.expo.dev/router/installation/), [src directory](https://docs.expo.dev/router/reference/src-directory/).
