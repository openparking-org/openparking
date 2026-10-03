import React, { useState } from 'react';
import { AlertCircle, LogIn } from 'lucide-react';
import { useAuthStore } from '../../store/authStore';

export const LoginPage: React.FC = () => {
  const { signIn, error, isSubmitting } = useAuthStore();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [touched, setTouched] = useState(false);

  const emailInvalid = touched && !/^\S+@\S+\.\S+$/.test(email);
  const passwordInvalid = touched && password.length === 0;

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setTouched(true);

    if (!/^\S+@\S+\.\S+$/.test(email) || password.length === 0) return;

    await signIn(email, password);
  };

  return (
    <div className="auth-shell">
      <form className="glass-panel auth-card" onSubmit={handleSubmit} noValidate>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '22px' }}>
          <div className="brand-icon">OP</div>
          <div>
            <div className="brand-title">OpenParking</div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Admin Operations</div>
          </div>
        </div>

        <h1 style={{ fontSize: '1.2rem', fontWeight: 700, marginBottom: '4px' }}>Sign in</h1>
        <p style={{ fontSize: '0.84rem', color: 'var(--text-secondary)', marginBottom: '22px' }}>
          Use your OpenParking account to continue.
        </p>

        {error && (
          <div className="form-alert form-alert-error" role="alert">
            <AlertCircle size={16} />
            <span>{error}</span>
          </div>
        )}

        <div className="field">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            autoComplete="username"
            value={email}
            aria-invalid={emailInvalid}
            aria-describedby={emailInvalid ? 'email-error' : undefined}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setEmail(e.target.value)}
          />
          {emailInvalid && <span className="field-error" id="email-error">Enter a valid email address.</span>}
        </div>

        <div className="field">
          <label htmlFor="password">Password</label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            value={password}
            aria-invalid={passwordInvalid}
            aria-describedby={passwordInvalid ? 'password-error' : undefined}
            onChange={(e: React.ChangeEvent<HTMLInputElement>) => setPassword(e.target.value)}
          />
          {passwordInvalid && <span className="field-error" id="password-error">Enter your password.</span>}
        </div>

        <button className="btn btn-primary" type="submit" disabled={isSubmitting} style={{ width: '100%', justifyContent: 'center' }}>
          <LogIn size={16} />
          {isSubmitting ? 'Signing in…' : 'Sign in'}
        </button>
      </form>
    </div>
  );
};
