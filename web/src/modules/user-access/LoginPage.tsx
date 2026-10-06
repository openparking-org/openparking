import React, { useState } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../../store/authStore';
import { apiClient, ApiError } from '../../lib/apiClient';
import { LogIn, AlertCircle, Eye, EyeOff, Loader2, Lock, ShieldCheck, Activity } from 'lucide-react';
import { ThemeToggle } from '../../components/ThemeToggle';

export const LoginPage: React.FC = () => {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  
  const navigate = useNavigate();
  const location = useLocation();
  const setAuth = useAuthStore(state => state.setAuth);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);

    try {
      const response = await apiClient.post('/api/users/login', { email: email.trim(), password });
      
      if (response.data) {
        if (!['ParkingAdmin', 'SystemAdmin'].includes(response.data.user?.role)) {
          setError('This workspace is for parking administrators. Sign in with an admin account.');
          return;
        }
        setAuth(response.data.token, response.data.user);
        
        // Redirect to intended page or dashboard
        const from = location.state?.from?.pathname || '/';
        navigate(from, { replace: true });
      }
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message || 'Invalid credentials');
      } else {
        setError('An unexpected error occurred. Please try again.');
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="auth">
      <aside className="auth-aside" aria-hidden="true">
        <div className="brand"><span className="brand-mark">P</span><span className="brand-name">OpenParking<small>Admin console</small></span></div>
        <div>
          <h2>Every bay, booking and decision in one place.</h2>
          <p>Run gates, review AI enforcement proposals, verify permits and keep pricing policy in check.</p>
          <div className="auth-lot">
            {['free', 'taken', 'taken', 'free', 'held', 'taken', 'taken', 'free', 'taken', 'taken', 'free', 'taken'].map((state, index) => <span key={index} className={state} />)}
          </div>
        </div>
        <div className="auth-features">
          <span><ShieldCheck size={14} />Human-in-the-loop AI</span>
          <span><Activity size={14} />Live occupancy</span>
          <span><Lock size={14} />Role-based access</span>
        </div>
      </aside>

      <main className="auth-main" style={{ position: 'relative' }}>
        <div className="auth-theme"><ThemeToggle /></div>
        <div className="auth-card">
          <h1>Sign in</h1>
          <p>Use your parking administrator account.</p>

          <form className="auth-form" onSubmit={handleSubmit}>
            {error && <div className="notice tone-crit" role="alert" style={{ margin: 0 }}><AlertCircle size={18} aria-hidden="true" /><div>{error}</div></div>}
            <div className="field">
              <label className="field-label" htmlFor="login-email">Email address</label>
              <input
                id="login-email"
                type="email"
                autoComplete="username"
                value={email}
                onChange={e => setEmail(e.target.value)}
                required
                autoFocus
                placeholder="admin@openparking.local"
              />
            </div>

            <div className="field">
              <label className="field-label" htmlFor="login-password">Password</label>
              <div className="password-field">
                <input
                  id="login-password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={password}
                  onChange={e => setPassword(e.target.value)}
                  required
                />
                <button type="button" onClick={() => setShowPassword(value => !value)} aria-label={showPassword ? 'Hide password' : 'Show password'} aria-pressed={showPassword}>
                  {showPassword ? <EyeOff size={17} /> : <Eye size={17} />}
                </button>
              </div>
            </div>

            <button type="submit" disabled={loading} className="btn btn-primary btn-lg btn-block">
              {loading ? <><Loader2 size={18} className="spin" aria-hidden="true" />Signing in…</> : <><LogIn size={18} aria-hidden="true" />Sign in</>}
            </button>
          </form>

          <p className="auth-foot"><Lock size={13} aria-hidden="true" />Driver accounts use the OpenParking mobile app.</p>
        </div>
      </main>
    </div>
  );
};
