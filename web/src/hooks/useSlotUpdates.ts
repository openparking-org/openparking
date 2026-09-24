import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export interface SlotStatusUpdate {
  slotId: string;
  status: 'Available' | 'Reserved' | 'Occupied' | 'Maintenance';
  updatedAt: string;
}

export function useSlotUpdates(zoneId: string) {
  const [updates, setUpdates] = useState<Record<string, SlotStatusUpdate>>({});
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!zoneId) return;

    const hubUrl = `${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/hubs/slots`;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.start()
      .then(() => {
        setIsConnected(true);
        connection.invoke('JoinZoneGroup', zoneId);
      })
      .catch((err: any) => {
        console.warn('SignalR connection failed (running in fallback mock mode):', err?.message);
      });

    connection.on('SlotStatusChanged', (update: SlotStatusUpdate) => {
      setUpdates((prev: Record<string, SlotStatusUpdate>) => ({ ...prev, [update.slotId]: update }));
    });

    return () => {
      if (connection.state === signalR.HubConnectionState.Connected) {
        connection.invoke('LeaveZoneGroup', zoneId);
        connection.stop();
      }
    };
  }, [zoneId]);

  return { updates, isConnected };
}
