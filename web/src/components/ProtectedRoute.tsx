import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { Lock } from 'lucide-react';
import { useAuthStore } from '../store/authStore';
import { EmptyState } from './PageTools';

interface ProtectedRouteProps {
  children: React.ReactNode;
  roles?: string[];
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, roles }) => {
  const { user, isAuthenticated, logout } = useAuthStore();
  const location = useLocation();

  if (!isAuthenticated()) {
    // Redirect to login but save the attempted URL
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (!user || (roles && !roles.includes(user.role))) {
    // Role not authorized
    return (
      <div className="card" style={{ maxWidth: 520, margin: '48px auto' }}>
        <EmptyState icon={Lock} title="You don’t have access to this page" action={<button className="btn btn-secondary" onClick={logout}>Sign out</button>}>
          This area is limited to {roles?.includes('ParkingAdmin') ? 'parking administrators' : 'system administrators'}. Ask a system administrator if you need access.
        </EmptyState>
      </div>
    );
  }

  return <>{children}</>;
};
