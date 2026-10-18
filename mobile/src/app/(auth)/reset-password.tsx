/**
 * Deep-link entry point for the recovery flow.
 *
 * A verification link sent through the Link channel lands here, but the whole UC-02 flow is one
 * four-step state machine, so this route renders the same screen as recover-account. The link only
 * carries an opaque recovery id; the proof still has to be typed in.
 */
export { default } from '@/features/auth/recovery/screens/RecoverAccountScreen';