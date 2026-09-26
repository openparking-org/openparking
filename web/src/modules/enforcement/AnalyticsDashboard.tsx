import React, { useState, useEffect, useCallback } from 'react';
import {
  DollarSign,
  TrendingUp,
  Users,
  Clock,
  ShieldAlert,
  CheckCircle,
  XCircle,
  RefreshCw,
  AlertTriangle,
  Activity,
  Lock,
  Key,
  Calendar,
  BarChart3
} from 'lucide-react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  BarChart,
  Bar,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip
} from 'recharts';
import {
  analyticsService,
  AnalyticsSummary,
  DailyRevenue,
  ZoneOccupancy,
  WeeklyViolation,
  WorkflowsSummary,
  WorkflowProposal,
  ApiError,
  getStoredToken,
  setStoredToken,
  clearStoredToken
} from '../../services/analyticsService';

export const AnalyticsDashboard: React.FC = () => {
  const [daysPeriod, setDaysPeriod] = useState<number>(30);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isRefreshing, setIsRefreshing] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [authError, setAuthError] = useState<boolean>(false);
  const [tokenInput, setTokenInput] = useState<string>('');
  const [showTokenModal, setShowTokenModal] = useState<boolean>(false);

  // Analytics states
  const [summary, setSummary] = useState<AnalyticsSummary | null>(null);
  const [revenueData, setRevenueData] = useState<DailyRevenue[]>([]);
  const [occupancyData, setOccupancyData] = useState<ZoneOccupancy[]>([]);
  const [violationsData, setViolationsData] = useState<WeeklyViolation[]>([]);
  const [workflowsSummary, setWorkflowsSummary] = useState<WorkflowsSummary | null>(null);
  const [pendingProposals, setPendingProposals] = useState<WorkflowProposal[]>([]);
  const [actionInProgressId, setActionInProgressId] = useState<string | null>(null);

  const fetchDashboardData = useCallback(async (isSilent = false) => {
    if (!isSilent) setIsLoading(true);
    else setIsRefreshing(true);

    setErrorMessage(null);
    setAuthError(false);

    try {
      const [
        summaryRes,
        revenueRes,
        occupancyRes,
        violationsRes,
        workflowSummaryRes,
        pendingRes
      ] = await Promise.all([
        analyticsService.getSummary(daysPeriod),
        analyticsService.getDailyRevenue(daysPeriod),
        analyticsService.getOccupancy(),
        analyticsService.getWeeklyViolations(8),
        analyticsService.getWorkflowsSummary(),
        analyticsService.getPendingWorkflows()
      ]);

      setSummary(summaryRes);
      setRevenueData(revenueRes || []);
      setOccupancyData(occupancyRes || []);
      setViolationsData(violationsRes || []);
      setWorkflowsSummary(workflowSummaryRes);
      setPendingProposals(pendingRes || []);
    } catch (err: any) {
      if (err instanceof ApiError) {
        if (err.status === 401 || err.status === 403) {
          setAuthError(true);
          setErrorMessage(err.message);
        } else {
          setErrorMessage(err.message);
        }
      } else {
        setErrorMessage(err?.message || 'An unexpected error occurred while loading analytics data.');
      }
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, [daysPeriod]);

  useEffect(() => {
    fetchDashboardData();
  }, [fetchDashboardData]);

  const handleWorkflowDecision = async (id: string, decision: 'APPROVE' | 'REJECT') => {
    setActionInProgressId(id);
    try {
      if (decision === 'APPROVE') {
        await analyticsService.approveWorkflow(id, 'Approved via Admin Analytics Dashboard');
      } else {
        await analyticsService.rejectWorkflow(id, 'Rejected via Admin Analytics Dashboard');
      }
      // Optimistically remove from pending proposals list
      setPendingProposals((prev) => prev.filter((p) => p.id !== id));
      // Refresh summaries
      fetchDashboardData(true);
    } catch (err: any) {
      alert(`Action failed: ${err?.message || 'Error executing decision'}`);
    } finally {
      setActionInProgressId(null);
    }
  };

  const handleSaveToken = (e: React.FormEvent) => {
    e.preventDefault();
    if (tokenInput.trim()) {
      setStoredToken(tokenInput.trim());
      setShowTokenModal(false);
      setTokenInput('');
      fetchDashboardData();
    }
  };

  const handleClearToken = () => {
    clearStoredToken();
    setShowTokenModal(false);
    fetchDashboardData();
  };

  const formatDuration = (totalMinutes: number): string => {
    if (!totalMinutes || totalMinutes <= 0) return '0m';
    const hours = Math.floor(totalMinutes / 60);
    const mins = Math.round(totalMinutes % 60);
    if (hours > 0) {
      return `${hours}h ${mins}m`;
    }
    return `${mins}m`;
  };

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'USD',
      minimumFractionDigits: 2
    }).format(amount || 0);
  };

  const overallOccupancyPct = occupancyData.length > 0
    ? Math.round(
        (occupancyData.reduce((acc, z) => acc + z.occupiedSlots, 0) /
          Math.max(1, occupancyData.reduce((acc, z) => acc + z.totalCapacity, 0))) * 100
      )
    : 0;

  return (
    <div style={{ display: 'flex', flexDirection: 'column', gap: '28px' }}>
      {/* Top Controls Toolbar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px' }}>
        <div>
          <h2 style={{ fontSize: '1.4rem', fontWeight: 700, letterSpacing: '-0.02em', display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Activity size={24} color="#6366f1" />
            Executive Analytics & Enforcement Overview
          </h2>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.88rem', marginTop: '4px' }}>
            Real-time aggregated telemetry, AI enforcement oversight, and revenue governance (design.md §21).
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
          {/* Period Range Filter */}
          <div style={{ display: 'flex', alignItems: 'center', gap: '6px', background: 'var(--bg-glass)', border: '1px solid var(--border-color)', borderRadius: 'var(--radius-md)', padding: '4px' }}>
            <Calendar size={16} color="var(--text-secondary)" style={{ marginLeft: '8px' }} />
            <button
              className={`btn ${daysPeriod === 7 ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '6px 12px', fontSize: '0.8rem', borderRadius: 'var(--radius-sm)' }}
              onClick={() => setDaysPeriod(7)}
            >
              7 Days
            </button>
            <button
              className={`btn ${daysPeriod === 30 ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '6px 12px', fontSize: '0.8rem', borderRadius: 'var(--radius-sm)' }}
              onClick={() => setDaysPeriod(30)}
            >
              30 Days
            </button>
            <button
              className={`btn ${daysPeriod === 90 ? 'btn-primary' : 'btn-secondary'}`}
              style={{ padding: '6px 12px', fontSize: '0.8rem', borderRadius: 'var(--radius-sm)' }}
              onClick={() => setDaysPeriod(90)}
            >
              90 Days
            </button>
          </div>

          {/* Refresh Button */}
          <button
            className="btn btn-secondary"
            onClick={() => fetchDashboardData(false)}
            disabled={isLoading || isRefreshing}
            title="Refresh analytics data"
          >
            <RefreshCw size={16} className={isRefreshing || isLoading ? 'animate-spin' : ''} />
            <span>{isRefreshing ? 'Refreshing...' : 'Refresh'}</span>
          </button>

          {/* Admin Token Modal Trigger */}
          <button
            className="btn btn-secondary"
            style={{ padding: '10px', borderColor: getStoredToken() ? 'rgba(16, 185, 129, 0.4)' : 'var(--border-color)' }}
            onClick={() => setShowTokenModal(!showTokenModal)}
            title="Configure Admin Authorization Bearer Token"
          >
            {getStoredToken() ? <Key size={16} color="#10b981" /> : <Lock size={16} color="var(--text-secondary)" />}
          </button>
        </div>
      </div>

      {/* Admin Token Configuration Banner/Modal */}
      {showTokenModal && (
        <div className="glass-panel" style={{ padding: '20px', border: '1px solid rgba(99, 102, 241, 0.4)', background: 'rgba(17, 24, 39, 0.95)' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Key size={18} color="#818cf8" />
              <strong style={{ fontSize: '0.95rem' }}>Admin Authorization Credentials (JWT Bearer)</strong>
            </div>
            <button className="btn btn-secondary" style={{ padding: '4px 8px', fontSize: '0.75rem' }} onClick={() => setShowTokenModal(false)}>
              Close
            </button>
          </div>
          <p style={{ fontSize: '0.82rem', color: 'var(--text-secondary)', marginBottom: '14px' }}>
            Protected analytics and workflow approval endpoints require a valid token with role <code style={{ color: '#818cf8' }}>ParkingAdmin</code> or <code style={{ color: '#818cf8' }}>SystemAdmin</code>.
          </p>
          <form onSubmit={handleSaveToken} style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
            <input
              type="text"
              placeholder="Paste Bearer token here..."
              value={tokenInput}
              onChange={(e) => setTokenInput(e.target.value)}
              style={{
                flex: 1,
                padding: '10px 14px',
                borderRadius: 'var(--radius-md)',
                background: 'rgba(255, 255, 255, 0.04)',
                border: '1px solid var(--border-color)',
                color: 'var(--text-primary)',
                fontSize: '0.85rem'
              }}
            />
            <button type="submit" className="btn btn-primary" style={{ padding: '10px 16px' }}>
              Save Token
            </button>
            {getStoredToken() && (
              <button type="button" className="btn btn-secondary" onClick={handleClearToken} style={{ color: '#ef4444' }}>
                Clear Token
              </button>
            )}
          </form>
        </div>
      )}

      {/* Error Alert Display */}
      {errorMessage && (
        <div
          className="glass-panel"
          style={{
            padding: '18px 24px',
            border: authError ? '1px solid rgba(245, 158, 11, 0.4)' : '1px solid rgba(239, 68, 68, 0.4)',
            background: authError ? 'rgba(245, 158, 11, 0.08)' : 'rgba(239, 68, 68, 0.08)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: '16px'
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <AlertTriangle size={24} color={authError ? '#f59e0b' : '#ef4444'} />
            <div>
              <div style={{ fontWeight: 600, fontSize: '0.92rem', color: authError ? '#f59e0b' : '#ef4444' }}>
                {authError ? 'Admin Authorization Required' : 'Failed to Load Analytics'}
              </div>
              <div style={{ fontSize: '0.82rem', color: 'var(--text-secondary)', marginTop: '2px' }}>
                {errorMessage}
              </div>
            </div>
          </div>
          <div style={{ display: 'flex', gap: '8px' }}>
            {authError && (
              <button className="btn btn-primary" style={{ padding: '6px 14px', fontSize: '0.8rem' }} onClick={() => setShowTokenModal(true)}>
                Provide Token
              </button>
            )}
            <button className="btn btn-secondary" style={{ padding: '6px 14px', fontSize: '0.8rem' }} onClick={() => fetchDashboardData(false)}>
              Retry
            </button>
          </div>
        </div>
      )}

      {/* Loading State Spinner */}
      {isLoading && !summary ? (
        <div className="glass-panel" style={{ padding: '64px 20px', textAlign: 'center' }}>
          <RefreshCw size={36} color="#6366f1" className="animate-spin" style={{ margin: '0 auto 16px auto', display: 'block' }} />
          <h3 style={{ fontSize: '1.15rem', fontWeight: 600 }}>Loading OpenParking Analytics Telemetry...</h3>
          <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem', marginTop: '6px' }}>
            Aggregating parking sessions, live occupancy views, and AI workflow runs.
          </p>
        </div>
      ) : (
        <>
          {/* Summary Metric Stat Cards (design.md §21.1) */}
          <div className="stats-grid">
            {/* Total Parking Sessions */}
            <div className="glass-panel stat-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="stat-label">Total Parking Sessions</span>
                <Users size={20} color="#3b82f6" />
              </div>
              <div className="stat-value">{summary ? summary.totalSessions.toLocaleString() : '0'}</div>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                <span className="badge badge-info">
                  {summary ? summary.activeSessions : 0} active now
                </span>
                <span className="badge badge-success">
                  {summary ? summary.completedSessions : 0} completed
                </span>
              </div>
            </div>

            {/* Total Revenue & Penalties */}
            <div className="glass-panel stat-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="stat-label">Total Revenue ({daysPeriod}d)</span>
                <DollarSign size={20} color="#10b981" />
              </div>
              <div className="stat-value">{summary ? formatCurrency(summary.totalRevenue) : '$0.00'}</div>
              <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap' }}>
                <span className="badge badge-warning">
                  Penalties: {summary ? formatCurrency(summary.totalPenalties) : '$0.00'}
                </span>
              </div>
            </div>

            {/* Average Stay Duration */}
            <div className="glass-panel stat-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="stat-label">Average Stay Duration</span>
                <Clock size={20} color="#8b5cf6" />
              </div>
              <div className="stat-value">
                {summary ? formatDuration(summary.avgDurationMinutes) : '0m'}
              </div>
              <span className="badge badge-info">Completed sessions</span>
            </div>

            {/* Enforcement Overstays & Workflows */}
            <div className="glass-panel stat-card">
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <span className="stat-label">Enforcement Overstays</span>
                <ShieldAlert size={20} color="#ef4444" />
              </div>
              <div className="stat-value">{summary ? summary.overstaySessions.toLocaleString() : '0'}</div>
              <span className="badge badge-danger">
                {summary ? summary.pendingWorkflowsCount : 0} pending review
              </span>
            </div>
          </div>

          {/* Visual Analytics Charts Grid (design.md §21.1 & §21.3) */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(460px, 1fr))', gap: '24px', marginBottom: '8px' }}>
            {/* Revenue Trend Chart */}
            <div className="glass-panel" style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 600 }}>Daily Revenue Trend</h3>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                    Revenue aggregate across all parking zones ({daysPeriod} days)
                  </p>
                </div>
                <DollarSign size={18} color="#10b981" />
              </div>

              {revenueData.length === 0 || revenueData.every((d) => d.revenue === 0) ? (
                <div style={{ height: '260px', display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', color: 'var(--text-muted)' }}>
                  <BarChart3 size={36} style={{ marginBottom: '8px', opacity: 0.5 }} />
                  <span>No revenue recorded for the selected date range</span>
                </div>
              ) : (
                <div style={{ width: '100%', height: '260px' }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={revenueData} margin={{ top: 10, right: 15, left: -10, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
                      <XAxis
                        dataKey="date"
                        tick={{ fill: '#94a3b8', fontSize: 11 }}
                        tickFormatter={(val) => {
                          const parts = val.split('-');
                          return parts.length === 3 ? `${parts[1]}/${parts[2]}` : val;
                        }}
                      />
                      <YAxis
                        tick={{ fill: '#94a3b8', fontSize: 11 }}
                        tickFormatter={(val) => `$${val}`}
                      />
                      <Tooltip
                        contentStyle={{
                          backgroundColor: '#0f172a',
                          borderColor: 'rgba(255,255,255,0.1)',
                          borderRadius: '8px',
                          color: '#f9fafb',
                          fontSize: '0.85rem'
                        }}
                        formatter={(val: any) => [`$${Number(val).toFixed(2)}`, 'Revenue']}
                        labelFormatter={(label) => `Date: ${label}`}
                      />
                      <Line
                        type="monotone"
                        dataKey="revenue"
                        stroke="#10b981"
                        strokeWidth={2.5}
                        dot={false}
                        activeDot={{ r: 5, fill: '#10b981' }}
                      />
                    </LineChart>
                  </ResponsiveContainer>
                </div>
              )}
            </div>

            {/* Live Occupancy by Zone */}
            <div className="glass-panel" style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 600 }}>Live Occupancy by Zone</h3>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                    Real-time slot utilization ({overallOccupancyPct}% network average)
                  </p>
                </div>
                <TrendingUp size={18} color="#6366f1" />
              </div>

              {occupancyData.length === 0 ? (
                <div style={{ height: '260px', display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', color: 'var(--text-muted)' }}>
                  <TrendingUp size={36} style={{ marginBottom: '8px', opacity: 0.5 }} />
                  <span>No zones or slots configured in system</span>
                </div>
              ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', maxHeight: '260px', overflowY: 'auto', paddingRight: '4px' }}>
                  {occupancyData.map((zone) => {
                    const isHighDemand = zone.occupancyPercentage >= 80;
                    const isMedium = zone.occupancyPercentage >= 50 && zone.occupancyPercentage < 80;
                    const barColor = isHighDemand ? '#ef4444' : isMedium ? '#f59e0b' : '#10b981';

                    return (
                      <div key={zone.zoneId} style={{ background: 'rgba(255,255,255,0.02)', padding: '12px 14px', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)' }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '6px' }}>
                          <span style={{ fontWeight: 600, fontSize: '0.88rem' }}>
                            {zone.zoneName} <span style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>({zone.zoneCode})</span>
                          </span>
                          <span style={{ fontWeight: 700, fontSize: '0.88rem', color: barColor }}>
                            {zone.occupancyPercentage}%
                          </span>
                        </div>
                        {/* Progress Bar */}
                        <div style={{ width: '100%', height: '8px', background: 'rgba(255,255,255,0.06)', borderRadius: '999px', overflow: 'hidden', marginBottom: '6px' }}>
                          <div
                            style={{
                              width: `${Math.min(100, Math.max(0, zone.occupancyPercentage))}%`,
                              height: '100%',
                              backgroundColor: barColor,
                              transition: 'width 0.4s ease'
                            }}
                          />
                        </div>
                        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.75rem', color: 'var(--text-secondary)' }}>
                          <span>{zone.occupiedSlots} Occupied / {zone.totalCapacity} Total</span>
                          <span>{zone.availableSlots} Available · {zone.reservedSlots} Reserved</span>
                        </div>
                      </div>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          {/* Lower Visual Grid: Violations Trend & AI Workflows Activity */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(460px, 1fr))', gap: '24px' }}>
            {/* Weekly Violations Trend */}
            <div className="glass-panel" style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 600 }}>Violations Trend (Weekly)</h3>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                    Overstay events detected per weekly rolling interval
                  </p>
                </div>
                <Clock size={18} color="#ef4444" />
              </div>

              {violationsData.length === 0 || violationsData.every((w) => w.count === 0) ? (
                <div style={{ height: '220px', display: 'flex', flexDirection: 'column', justifyContent: 'center', alignItems: 'center', color: 'var(--text-muted)' }}>
                  <ShieldAlert size={36} style={{ marginBottom: '8px', opacity: 0.5 }} />
                  <span>No overstay violations recorded in recent weeks</span>
                </div>
              ) : (
                <div style={{ width: '100%', height: '220px' }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart data={violationsData} margin={{ top: 10, right: 15, left: -20, bottom: 0 }}>
                      <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
                      <XAxis dataKey="weekLabel" tick={{ fill: '#94a3b8', fontSize: 11 }} />
                      <YAxis allowDecimals={false} tick={{ fill: '#94a3b8', fontSize: 11 }} />
                      <Tooltip
                        contentStyle={{
                          backgroundColor: '#0f172a',
                          borderColor: 'rgba(255,255,255,0.1)',
                          borderRadius: '8px',
                          color: '#f9fafb',
                          fontSize: '0.85rem'
                        }}
                        formatter={(val: any) => [`${val} violations`, 'Overstays']}
                      />
                      <Bar dataKey="count" fill="#ef4444" radius={[4, 4, 0, 0]} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              )}
            </div>

            {/* AI Workflow Run Activity Breakdown */}
            <div className="glass-panel" style={{ padding: '24px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '18px' }}>
                <div>
                  <h3 style={{ fontSize: '1.1rem', fontWeight: 600 }}>AI Workflow Execution Status</h3>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.8rem' }}>
                    LangGraph multi-agent lifecycle distribution (design.md §21.1)
                  </p>
                </div>
                <Activity size={18} color="#8b5cf6" />
              </div>

              <div style={{ display: 'grid', gridTemplateColumns: 'repeat(2, 1fr)', gap: '14px' }}>
                <div style={{ padding: '14px', borderRadius: 'var(--radius-md)', background: 'rgba(16, 185, 129, 0.08)', border: '1px solid rgba(16, 185, 129, 0.2)' }}>
                  <div style={{ fontSize: '0.78rem', color: '#10b981', fontWeight: 600, textTransform: 'uppercase' }}>Approved</div>
                  <div style={{ fontSize: '1.6rem', fontWeight: 700, margin: '4px 0' }}>
                    {workflowsSummary ? workflowsSummary.approved : 0}
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Admin authorized</div>
                </div>

                <div style={{ padding: '14px', borderRadius: 'var(--radius-md)', background: 'rgba(59, 130, 246, 0.08)', border: '1px solid rgba(59, 130, 246, 0.2)' }}>
                  <div style={{ fontSize: '0.78rem', color: '#3b82f6', fontWeight: 600, textTransform: 'uppercase' }}>Auto-Approved</div>
                  <div style={{ fontSize: '1.6rem', fontWeight: 700, margin: '4px 0' }}>
                    {workflowsSummary ? workflowsSummary.autoApproved : 0}
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Confidence &gt; 95%</div>
                </div>

                <div style={{ padding: '14px', borderRadius: 'var(--radius-md)', background: 'rgba(245, 158, 11, 0.08)', border: '1px solid rgba(245, 158, 11, 0.2)' }}>
                  <div style={{ fontSize: '0.78rem', color: '#f59e0b', fontWeight: 600, textTransform: 'uppercase' }}>Pending Approval</div>
                  <div style={{ fontSize: '1.6rem', fontWeight: 700, margin: '4px 0' }}>
                    {workflowsSummary ? workflowsSummary.pending : 0}
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>Awaiting human-in-the-loop</div>
                </div>

                <div style={{ padding: '14px', borderRadius: 'var(--radius-md)', background: 'rgba(239, 68, 68, 0.08)', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
                  <div style={{ fontSize: '0.78rem', color: '#ef4444', fontWeight: 600, textTransform: 'uppercase' }}>Rejected / Failed</div>
                  <div style={{ fontSize: '1.6rem', fontWeight: 700, margin: '4px 0' }}>
                    {workflowsSummary ? workflowsSummary.rejected + workflowsSummary.failed : 0}
                  </div>
                  <div style={{ fontSize: '0.72rem', color: 'var(--text-muted)' }}>
                    {workflowsSummary?.rejected || 0} rejected · {workflowsSummary?.failed || 0} safe fail
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Real AI Workflow Approval Queue (Human-in-the-Loop) */}
          <div className="glass-panel" style={{ padding: '24px', marginTop: '4px' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '16px', flexWrap: 'wrap', gap: '10px' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                <ShieldAlert size={22} color="#f59e0b" />
                <div>
                  <h3 style={{ fontSize: '1.2rem', fontWeight: 600 }}>AI Workflow Approval Queue (Human-in-the-Loop)</h3>
                  <p style={{ color: 'var(--text-secondary)', fontSize: '0.85rem' }}>
                    Agent proposals requiring administrator validation before applying production penalties or surges.
                  </p>
                </div>
              </div>
              <span className="badge badge-warning">
                {pendingProposals.length} Pending Actions
              </span>
            </div>

            {pendingProposals.length === 0 ? (
              <div style={{ padding: '36px 20px', textAlign: 'center', color: 'var(--text-secondary)' }}>
                <CheckCircle size={32} color="#10b981" style={{ margin: '0 auto 10px auto', display: 'block' }} />
                <div style={{ fontWeight: 600, color: 'var(--text-primary)' }}>No pending AI agent proposals.</div>
                <div style={{ fontSize: '0.82rem', marginTop: '4px' }}>All active workflows have been verified and processed.</div>
              </div>
            ) : (
              <div style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
                {pendingProposals.map((item: WorkflowProposal) => {
                  const isProcessing = actionInProgressId === item.id;

                  return (
                    <div
                      key={item.id}
                      style={{
                        padding: '18px 20px',
                        borderRadius: 'var(--radius-md)',
                        backgroundColor: 'rgba(255, 255, 255, 0.02)',
                        border: '1px solid var(--border-color)',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'center',
                        flexWrap: 'wrap',
                        gap: '14px'
                      }}
                    >
                      <div style={{ flex: 1, minWidth: '280px' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '6px', flexWrap: 'wrap' }}>
                          <span className="badge badge-info">{item.workflowType}</span>
                          <span style={{ fontWeight: 600, fontSize: '0.95rem' }}>
                            {item.objective}
                          </span>
                          <span style={{ fontSize: '0.78rem', color: 'var(--text-muted)' }}>
                            {new Date(item.triggeredAt).toLocaleString()}
                          </span>
                        </div>
                        <div style={{ fontSize: '0.85rem', color: 'var(--text-secondary)', display: 'flex', gap: '16px', flexWrap: 'wrap' }}>
                          {item.sessionId && (
                            <span>Session: <strong style={{ color: '#818cf8' }}>{item.sessionId.substring(0, 8)}...</strong></span>
                          )}
                          {item.zoneId && (
                            <span>Zone: <strong style={{ color: '#818cf8' }}>{item.zoneId.substring(0, 8)}...</strong></span>
                          )}
                          <span>Status: <strong style={{ color: '#f59e0b' }}>{item.status}</strong></span>
                        </div>
                      </div>

                      <div style={{ display: 'flex', gap: '10px' }}>
                        <button
                          className="btn btn-primary"
                          style={{ background: '#10b981', boxShadow: '0 4px 12px rgba(16, 185, 129, 0.3)' }}
                          onClick={() => handleWorkflowDecision(item.id, 'APPROVE')}
                          disabled={isProcessing}
                        >
                          <CheckCircle size={16} />
                          {isProcessing ? 'Processing...' : 'Approve'}
                        </button>
                        <button
                          className="btn btn-secondary"
                          style={{ borderColor: 'rgba(239, 68, 68, 0.4)', color: '#ef4444' }}
                          onClick={() => handleWorkflowDecision(item.id, 'REJECT')}
                          disabled={isProcessing}
                        >
                          <XCircle size={16} />
                          {isProcessing ? 'Processing...' : 'Reject'}
                        </button>
                      </div>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        </>
      )}
    </div>
  );
};
