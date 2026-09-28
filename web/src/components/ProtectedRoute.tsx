import React from 'react';
import { Navigate, useLocation } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';

interface ProtectedRouteProps {
  children: React.ReactNode;
  roles?: string[];
}

export const ProtectedRoute: React.FC<ProtectedRouteProps> = ({ children, roles }) => {
  const { user, isAuthenticated } = useAuthStore();
  const location = useLocation();

  if (!isAuthenticated()) {
    // Redirect to login but save the attempted URL
    return <Navigate to="/login" state={{ from: location }} replace />;
  }

  if (roles && user && !roles.includes(user.role)) {
    // Role not authorized
    return (
      <div style={{ padding: '2rem', textAlign: 'center' }}>
        <h2>Unauthorized Access</h2>
        <p>You do not have permission to view this page.</p>
        <Navigate to="/" replace />
      </div>
    );
  }

  return <>{children}</>;
};
