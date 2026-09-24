import React, { useState } from 'react';
import { ShieldAlert, CheckCircle, XCircle, TrendingUp, Users, DollarSign, Clock } from 'lucide-react';

interface AIProposal {
  id: string;
  workflowType: string;
  zone: string;
  proposalSummary: string;
  amountOrMultiplier: string;
  confidence: number;
  reason: string;
}

export const AnalyticsDashboard: React.FC = () => {
  const [proposals, setProposals] = useState<AIProposal[]>([
    {
      id: 'prop-001',
      workflowType: 'DYNAMIC_PRICING',
      zone: 'Zone A - Financial District',
      proposalSummary: 'Surge pricing proposed due to 92% occupancy velocity',
      amountOrMultiplier: '2.0x ($10.00/hr)',
      confidence: 0.94,
      reason: 'Predicted stadium event traffic converging within 45 minutes.'
    },
    {
      id: 'prop-002',
      workflowType: 'OVERSTAY_ENFORCEMENT',
      zone: 'Zone B - Medical Center',
      proposalSummary: 'Overstay penalty for vehicle #WP-CAD-8931 (145 mins)',
      amountOrMultiplier: '$75.00',
      confidence: 0.98,
      reason: 'Exceeded 15 min grace window; validated against local cap.'
    }
  ]);

  const handleDecision = (id: string, decision: 'APPROVE' | 'REJECT') => {
    setProposals(proposals.filter((p: AIProposal) => p.id !== id));
    alert(`AI Workflow proposal ${id} has been ${decision}D.`);
  };

  return (
    <div>
      {/* Metric Stat Cards */}
      <div className="stats-grid">
        <div className="glass-panel stat-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="stat-label">Total Revenue (Today)</span>
            <DollarSign size={20} color="#10b981" />
          </div>
          <div className="stat-value">$4,289.50</div>
          <span className="badge badge-success">+14.2% from yesterday</span>
        </div>

        <div className="glass-panel stat-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="stat-label">Live Occupancy</span>
            <TrendingUp size={20} color="#6366f1" />
          </div>
          <div className="stat-value">84.2%</div>
          <span className="badge badge-warning">High demand threshold</span>
        </div>

        <div className="glass-panel stat-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="stat-label">Active Parking Sessions</span>
            <Users size={20} color="#3b82f6" />
          </div>
          <div className="stat-value">312</div>
          <span className="badge badge-info">Across 4 zones</span>
        </div>

        <div className="glass-panel stat-card">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="stat-label">Enforcement Overstays</span>
            <Clock size={20} color="#ef4444" />
          </div>
          <div className="stat-value">7</div>
          <span className="badge badge-danger">2 pending approval</span>
        </div>
      </div>

      {/* Human-in-the-Loop AI Proposals Queue */}
      <div className="glass-panel" style={{ padding: '24px', marginBottom: '32px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '16px' }}>
          <ShieldAlert size={22} color="#f59e0b" />
          <div>
            <h3 style={{ fontSize: '1.2rem', fontWeight: 600 }}>AI Workflow Approval Queue (Human-in-the-Loop)</h3>
            <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
              LangGraph multi-agent proposals requiring manual validation before production execution.
            </p>
          </div>
        </div>

        {proposals.length === 0 ? (
          <div style={{ padding: '32px', textAlign: 'center', color: 'var(--text-secondary)' }}>
            No pending AI agent proposals. All active workflows are verified.
          </div>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
            {proposals.map((item: AIProposal) => (
              <div
                key={item.id}
                style={{
                  padding: '18px',
                  borderRadius: 'var(--radius-md)',
                  backgroundColor: 'rgba(255, 255, 255, 0.02)',
                  border: '1px solid var(--border-color)',
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'center'
                }}
              >
                <div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px' }}>
                    <span className="badge badge-info">{item.workflowType}</span>
                    <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>{item.zone}</span>
                    <span style={{ fontSize: '0.8rem', color: '#10b981' }}>({Math.round(item.confidence * 100)}% AI Confidence)</span>
                  </div>
                  <div style={{ fontSize: '0.9rem', color: 'var(--text-primary)', marginBottom: '4px' }}>
                    {item.proposalSummary} — <strong style={{ color: '#818cf8' }}>{item.amountOrMultiplier}</strong>
                  </div>
                  <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)' }}>
                    {item.reason}
                  </div>
                </div>

                <div style={{ display: 'flex', gap: '10px' }}>
                  <button
                    className="btn btn-primary"
                    style={{ background: '#10b981', boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)' }}
                    onClick={() => handleDecision(item.id, 'APPROVE')}
                  >
                    <CheckCircle size={16} /> Approve
                  </button>
                  <button
                    className="btn btn-secondary"
                    style={{ borderColor: 'rgba(239, 68, 68, 0.4)', color: '#ef4444' }}
                    onClick={() => handleDecision(item.id, 'REJECT')}
                  >
                    <XCircle size={16} /> Reject
                  </button>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>
    </div>
  );
};
