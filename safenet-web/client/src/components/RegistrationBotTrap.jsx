// A quiet honeypot for indiscriminate form-filling bots. Server validation and
// per-identity/network rate limits remain the enforcement; this is not CAPTCHA.
export default function RegistrationBotTrap({ value, onChange }) {
  return <div aria-hidden="true" className="bot-trap-field">
    <label>Website<input name="website" value={value} onChange={onChange}
      tabIndex={-1} autoComplete="off" /></label>
  </div>;
}
