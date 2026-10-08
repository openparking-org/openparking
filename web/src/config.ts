export const config = {
  apiUrl: import.meta.env.PROD ? '' : (import.meta.env.VITE_API_BASE_URL || import.meta.env.VITE_API_URL || 'http://localhost:5000').replace(/\/+$/, ''),
  aiApiUrl: import.meta.env.VITE_AI_BASE_URL || 'http://localhost:8000',
};
