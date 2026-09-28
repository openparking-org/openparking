import React, { useState, useEffect } from 'react';
import { apiClient } from '../../lib/apiClient';
import { CheckCircle, XCircle, FileText, AlertTriangle, ShieldCheck } from 'lucide-react';

interface Permit {
  id: string;
  userId: string;
  permitNumber: string;
  documentImageUrl: string;
  jurisdiction: string;
  expiryDate: string;
  status: 'Pending' | 'Approved' | 'Rejected';
  createdAt: string;
  user?: {
    fullName: string;
  };
}

export const PermitsPage: React.FC = () => {
  const [permits, setPermits] = useState<Permit[]>([]);
  const [loading, setLoading] = useState(true);
  const [validating, setValidating] = useState<Record<string, boolean>>({});
  const [aiResults, setAiResults] = useState<Record<string, any>>({});
  const [filter, setFilter] = useState<'All' | 'Pending' | 'Approved' | 'Rejected'>('Pending');

  useEffect(() => {
    fetchPermits();
  }, []);

  const fetchPermits = async () => {
    setLoading(true);
    try {
      // In a real app we'd fetch all or pending based on filter, for now mock it if no backend
      const response = await apiClient.get('/api/users/permits/pending');
      setPermits(response.data || []);
    } catch (error) {
      console.error('Failed to fetch permits:', error);
    } finally {
      setLoading(false);
    }
  };

  const validateWithAI = async (permit: Permit) => {
    setValidating(prev => ({ ...prev, [permit.id]: true }));
    try {
      // Direct call to AI service endpoint
      const response = await apiClient.post('/ai/permits/validate', {
        permit_number: permit.permitNumber,
        expiry_date: permit.expiryDate,
        jurisdiction: permit.jurisdiction,
        document_image_url: permit.documentImageUrl
      });
      
      setAiResults(prev => ({ ...prev, [permit.id]: response }));
    } catch (error) {
      console.error('AI validation failed:', error);
      alert('AI validation service is currently unavailable.');
    } finally {
      setValidating(prev => ({ ...prev, [permit.id]: false }));
    }
  };

  const handleReview = async (permitId: string, decision: 'Approved' | 'Rejected') => {
    try {
      await apiClient.patch(`/api/users/permits/${permitId}/review`, {
        decision,
        notes: aiResults[permitId] ? `AI Confidence: ${aiResults[permitId].confidence}` : 'Manual review'
      });
      
      // Remove or update from list
      setPermits(prev => prev.filter(p => p.id !== permitId));
    } catch (error) {
      console.error('Review failed:', error);
      alert('Failed to submit review.');
    }
  };

  const filteredPermits = permits.filter(p => filter === 'All' || p.status === filter);

  return (
    <div className="glass-panel" style={{ padding: '28px' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '24px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          <div className="brand-icon" style={{ width: '42px', height: '42px', background: 'rgba(16, 185, 129, 0.15)', color: '#10b981' }}>
            <FileText size={22} />
          </div>
          <div>
            <h2 style={{ fontSize: '1.35rem', fontWeight: 700 }}>Disability Permit Review</h2>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem' }}>
              {permits.filter(p => p.status === 'Pending').length} permits pending AI validation
            </p>
          </div>
        </div>

        <div style={{ display: 'flex', gap: '8px' }}>
          {['Pending', 'All'].map(f => (
            <button 
              key={f}
              onClick={() => setFilter(f as any)}
              className={`btn ${filter === f ? 'btn-primary' : 'btn-secondary'}`}
            >
              {f}
            </button>
          ))}
        </div>
      </div>

      {loading ? (
        <p>Loading permits...</p>
      ) : filteredPermits.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '40px', color: 'var(--text-secondary)' }}>
          No permits found for the selected filter.
        </div>
      ) : (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          {filteredPermits.map(permit => (
            <div key={permit.id} style={{ 
              border: '1px solid var(--border-color)', 
              borderRadius: '8px', 
              padding: '20px',
              background: 'rgba(0,0,0,0.2)'
            }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
                <div>
                  <h3 style={{ margin: '0 0 8px 0' }}>USER: {permit.user?.fullName || 'Unknown User'}</h3>
                  <div style={{ color: 'var(--text-secondary)', fontSize: '0.9rem', display: 'flex', flexDirection: 'column', gap: '4px' }}>
                    <span><strong>Permit:</strong> {permit.permitNumber}</span>
                    <span><strong>Jurisdiction:</strong> {permit.jurisdiction}</span>
                    <span><strong>Expiry:</strong> {new Date(permit.expiryDate).toLocaleDateString()}</span>
                  </div>
                </div>

                <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'flex-end', gap: '12px' }}>
                  {aiResults[permit.id] ? (
                    <div style={{ 
                      background: aiResults[permit.id].valid ? 'rgba(16, 185, 129, 0.1)' : 'rgba(239, 68, 68, 0.1)',
                      border: `1px solid ${aiResults[permit.id].valid ? '#10b981' : '#ef4444'}`,
                      padding: '12px',
                      borderRadius: '6px',
                      width: '280px'
                    }}>
                      <h4 style={{ margin: '0 0 8px 0', fontSize: '0.9rem', display: 'flex', alignItems: 'center', gap: '6px' }}>
                        <ShieldCheck size={16} /> AI Validation Result
                      </h4>
                      <div style={{ fontSize: '0.85rem' }}>
                        <div>Confidence: <strong>{aiResults[permit.id].confidence}</strong> {aiResults[permit.id].valid ? '✅ AUTO-APPROVED' : '⚠️ HIGH RISK'}</div>
                        {!aiResults[permit.id].valid && (
                          <div style={{ color: '#ef4444', marginTop: '4px', fontSize: '0.8rem' }}>
                            {aiResults[permit.id].reason}
                          </div>
                        )}
                      </div>
                    </div>
                  ) : (
                    <button 
                      onClick={() => validateWithAI(permit)}
                      disabled={validating[permit.id]}
                      className="btn btn-secondary"
                    >
                      {validating[permit.id] ? 'Analyzing...' : 'Run AI Validation'}
                    </button>
                  )}

                  <div style={{ display: 'flex', gap: '8px', marginTop: 'auto' }}>
                    <button onClick={() => handleReview(permit.id, 'Approved')} className="btn btn-primary" style={{ background: '#10b981' }}>
                      <CheckCircle size={16} style={{ marginRight: '6px' }} /> Approve
                    </button>
                    <button onClick={() => handleReview(permit.id, 'Rejected')} className="btn btn-secondary" style={{ color: '#ef4444', borderColor: '#ef4444' }}>
                      <XCircle size={16} style={{ marginRight: '6px' }} /> Reject
                    </button>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
