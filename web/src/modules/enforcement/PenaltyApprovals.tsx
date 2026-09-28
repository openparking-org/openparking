import { useState, useEffect, useContext } from 'react';
import { useAuthStore } from '../../store/authStore';
import { SettingsContext } from '../../App';
import { CheckCircle, XCircle } from 'lucide-react';

interface Workflow {
  id: string;
  workflowType: string;
  status: string;
  objective: string;
  sessionId?: string;
  zoneId?: string;
  createdAt: string;
}

interface WorkflowDetail extends Workflow {
  stepResultsJson: string;
}

const API_BASE = 'http://localhost:5000'; // Fallback if no .env

export function PenaltyApprovals() {
  const { token } = useAuthStore();
  const { settings } = useContext(SettingsContext);
  const [workflows, setWorkflows] = useState<Workflow[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [selectedWorkflow, setSelectedWorkflow] = useState<WorkflowDetail | null>(null);
  const [rejectReason, setRejectReason] = useState('');
  const [processing, setProcessing] = useState(false);

  const fetchWorkflows = async () => {
    try {
      setLoading(true);
      const res = await fetch(`${API_BASE}/api/agent/workflows/pending`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!res.ok) throw new Error('Failed to fetch pending workflows');
      const json = await res.json();
      setWorkflows(json.data || []);
    } catch (err: any) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchWorkflows();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token]);

  const handleSelect = async (id: string) => {
    try {
      const res = await fetch(`${API_BASE}/api/agent/workflows/${id}`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!res.ok) throw new Error('Failed to fetch details');
      const json = await res.json();
      setSelectedWorkflow(json.data);
      setRejectReason('');
    } catch (err: any) {
      alert(err.message);
    }
  };

  const handleApprove = async (id: string) => {
    if (!window.confirm('Are you sure you want to approve this penalty?')) return;
    try {
      setProcessing(true);
      const res = await fetch(`${API_BASE}/api/agent/workflows/${id}/approve`, {
        method: 'POST',
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!res.ok) {
        const errorData = await res.json();
        throw new Error(errorData.error?.message || 'Failed to approve');
      }
      setSelectedWorkflow(null);
      fetchWorkflows();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setProcessing(false);
    }
  };

  const handleReject = async (id: string) => {
    if (!rejectReason.trim()) {
      alert('Please provide a reason for rejection.');
      return;
    }
    if (!window.confirm('Are you sure you want to reject this penalty?')) return;
    try {
      setProcessing(true);
      const res = await fetch(`${API_BASE}/api/agent/workflows/${id}/reject`, {
        method: 'POST',
        headers: { 
          Authorization: `Bearer ${token}`,
          'Content-Type': 'application/json'
        },
        body: JSON.stringify({ reason: rejectReason })
      });
      if (!res.ok) {
        const errorData = await res.json();
        throw new Error(errorData.error?.message || 'Failed to reject');
      }
      setSelectedWorkflow(null);
      fetchWorkflows();
    } catch (err: any) {
      alert(err.message);
    } finally {
      setProcessing(false);
    }
  };

  const parseStepResults = (jsonStr: string) => {
    try {
      if (!jsonStr) return null;
      return JSON.parse(jsonStr);
    } catch {
      return null;
    }
  };

  if (loading && workflows.length === 0) return <div>Loading AI proposals...</div>;
  if (error) return <div style={{ color: 'red' }}>Error: {error}</div>;

  return (
    <div>
      <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '16px' }}>
        <h1 style={{ margin: 0 }}>Penalty Approvals (AI Queue)</h1>
      </div>
      <hr style={{ marginBottom: '24px' }} />

      <div style={{ display: 'flex', gap: '24px' }}>
        {/* Left Column: List */}
        <div style={{ flex: 1 }}>
          <h3>Pending Review ({workflows.length})</h3>
          {workflows.length === 0 ? (
            <p style={{ color: 'var(--text-secondary)' }}>No workflows awaiting approval.</p>
          ) : (
            <ul style={{ listStyle: 'none', padding: 0, display: 'flex', flexDirection: 'column', gap: '12px' }}>
              {workflows.map(wf => (
                <li 
                  key={wf.id}
                  onClick={() => handleSelect(wf.id)}
                  style={{
                    padding: '16px',
                    border: `1px solid ${selectedWorkflow?.id === wf.id ? '#3b82f6' : '#e2e8f0'}`,
                    borderRadius: '8px',
                    cursor: 'pointer',
                    background: selectedWorkflow?.id === wf.id ? 'rgba(59, 130, 246, 0.05)' : '#fff'
                  }}
                >
                  <div style={{ fontWeight: 'bold', marginBottom: '4px' }}>{wf.objective}</div>
                  <div style={{ fontSize: '0.85rem', color: 'gray' }}>
                    Type: {wf.workflowType} | Time: {new Date(wf.createdAt).toLocaleString()}
                  </div>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Right Column: Detail */}
        {selectedWorkflow && (
          <div style={{ flex: 1, padding: '20px', border: '1px solid #e2e8f0', borderRadius: '8px', background: '#f8fafc' }}>
            <h3 style={{ marginTop: 0 }}>Workflow Details</h3>
            <div style={{ marginBottom: '16px', fontSize: '0.9rem' }}>
              <strong>Objective:</strong> {selectedWorkflow.objective}<br/>
              <strong>Session ID:</strong> {selectedWorkflow.sessionId}<br/>
              <strong>Date:</strong> {new Date(selectedWorkflow.createdAt).toLocaleString()}
            </div>

            <div style={{ background: '#fff', padding: '16px', borderRadius: '8px', border: '1px solid #e2e8f0', marginBottom: '16px' }}>
              <h4 style={{ margin: '0 0 12px 0' }}>AI Penalty Proposal</h4>
              {(() => {
                const results = parseStepResults(selectedWorkflow.stepResultsJson);
                if (!results) return <span style={{ color: 'gray' }}>No structured proposal generated by AI.</span>;
                
                return (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                    <div style={{ fontSize: '1.2rem', fontWeight: 'bold', color: '#ef4444' }}>
                      Proposed Fine: {settings.defaultCurrency} {results.amount ?? results.proposed_amount ?? '50.00'}
                    </div>
                    <div>
                      <strong>Billable Hours:</strong> {results.billable_hours ?? 'N/A'}
                    </div>
                    <div style={{ background: '#f1f5f9', padding: '12px', borderRadius: '4px', fontSize: '0.9rem' }}>
                      <strong>AI Justification:</strong><br/>
                      {results.reason || 'No specific reason provided.'}
                    </div>
                  </div>
                );
              })()}
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
              <input 
                type="text" 
                placeholder="Reason for rejection (required if rejecting)" 
                value={rejectReason}
                onChange={e => setRejectReason(e.target.value)}
                style={{ padding: '8px', width: '100%', borderRadius: '4px', border: '1px solid #ccc' }}
              />
              <div style={{ display: 'flex', gap: '12px' }}>
                <button 
                  disabled={processing}
                  onClick={() => handleApprove(selectedWorkflow.id)}
                  style={{
                    flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px',
                    background: '#10b981', color: '#fff', padding: '10px', borderRadius: '6px', border: 'none', cursor: 'pointer'
                  }}
                >
                  <CheckCircle size={18} /> Approve Fine
                </button>
                <button 
                  disabled={processing || !rejectReason.trim()}
                  onClick={() => handleReject(selectedWorkflow.id)}
                  style={{
                    flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '8px',
                    background: '#ef4444', color: '#fff', padding: '10px', borderRadius: '6px', border: 'none', cursor: 'pointer'
                  }}
                >
                  <XCircle size={18} /> Reject Fine
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
