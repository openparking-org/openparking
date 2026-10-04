import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';

export interface SlotStatusUpdate {
  slotId: string;
  status: 'Available' | 'Reserved' | 'Occupied' | 'Maintenance';
  updatedAt?: string;
}

export function useSlotUpdates(zoneId: string) {
  const [updates, setUpdates] = useState<Record<string, SlotStatusUpdate>>({});
  const [isConnected, setIsConnected] = useState(false);

  useEffect(() => {
    if (!zoneId) return;
    setUpdates({});
    setIsConnected(false);
    let disposed = false;

    const hubUrl = `${import.meta.env.VITE_API_URL || 'http://localhost:5000'}/hubs/slots`;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl)
      .withAutomaticReconnect()
      .build();

    connection.onreconnecting(() => setIsConnected(false));
    connection.onclose(() => setIsConnected(false));
    connection.onreconnected(async () => {
      if (disposed) return;
      try {
        await connection.invoke('JoinZoneGroup', zoneId);
        setIsConnected(true);
      } catch (err) {
        console.warn('SignalR zone subscription failed:', err);
      }
    });

    connection.start()
      .then(async () => {
        if (disposed) {
          await connection.stop();
          return;
        }
        await connection.invoke('JoinZoneGroup', zoneId);
        setIsConnected(true);
      })
      .catch((err: any) => {
        console.warn('SignalR connection failed (running in fallback mock mode):', err?.message);
      });

    connection.on('SlotUpdated', (update: SlotStatusUpdate) => {
      setUpdates((prev: Record<string, SlotStatusUpdate>) => ({ ...prev, [update.slotId]: update }));
    });

    return () => {
      disposed = true;
      void connection.stop().catch((err) => console.warn('SignalR cleanup failed:', err));
    };
  }, [zoneId]);

  return { updates, isConnected };
}
