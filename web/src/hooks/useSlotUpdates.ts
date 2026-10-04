import { useEffect, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { config } from '../config';
import { useAuthStore } from '../store/authStore';

export interface SlotStatusUpdate {
  slotId: string;
  status: 'Available' | 'Reserved' | 'Occupied' | 'Maintenance';
  updatedAt?: string;
}

export function useSlotUpdates(zoneId: string) {
  const [updates, setUpdates] = useState<Record<string, SlotStatusUpdate>>({});
  const [isConnected, setIsConnected] = useState(false);
  const [connectionRevision, setConnectionRevision] = useState(0);

  useEffect(() => {
    if (!zoneId) return;
    setUpdates({});
    setIsConnected(false);
    let disposed = false;

    const hubUrl = `${config.apiUrl}/hubs/slots`;
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => useAuthStore.getState().token || '' })
      .withAutomaticReconnect()
      .build();

    connection.onreconnecting(() => setIsConnected(false));
    connection.onclose(() => setIsConnected(false));
    connection.onreconnected(async () => {
      if (disposed) return;
      try {
        await connection.invoke('JoinZoneGroup', zoneId);
        setIsConnected(true);
        setConnectionRevision(value => value + 1);
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
        console.warn('SignalR connection failed:', err?.message);
      });

    connection.on('SlotUpdated', (update: SlotStatusUpdate) => {
      setUpdates((prev: Record<string, SlotStatusUpdate>) => ({ ...prev, [update.slotId]: update }));
    });

    return () => {
      disposed = true;
      void connection.stop().catch((err) => console.warn('SignalR cleanup failed:', err));
    };
  }, [zoneId]);

  return { updates, isConnected, connectionRevision };
}
