import { useEffect, useMemo, useRef, useState } from 'react';
import { GoogleMap, MarkerF, useJsApiLoader } from '@react-google-maps/api';

export interface EntranceLocation { lat: number; lng: number }
interface Props { position: EntranceLocation | null; onChange: (position: EntranceLocation) => void; disabled: boolean }
const defaultCenter = { lat: 6.9271, lng: 79.8612 };
const mapStyle = { width: '100%', height: '360px' };
const mapOptions = { streetViewControl: false, fullscreenControl: false, clickableIcons: false };

function EntranceMap({ position, onChange, disabled, apiKey }: Props & { apiKey: string }) {
  const { isLoaded, loadError } = useJsApiLoader({ id: 'openparking-google-maps', googleMapsApiKey: apiKey });
  const lat = position?.lat, lng = position?.lng;
  const center = useMemo(() => lat !== undefined && lng !== undefined ? { lat, lng } : defaultCenter, [lat, lng]);
  const select = (event: google.maps.MapMouseEvent) => {
    if (!disabled && event.latLng) onChange({ lat: event.latLng.lat(), lng: event.latLng.lng() });
  };
  if (loadError) return <p role="status">The map could not load. Use your location or enter coordinates below.</p>;
  if (!isLoaded) return <p role="status">Loading entrance map…</p>;
  return <GoogleMap mapContainerStyle={mapStyle} center={center} zoom={position ? 18 : 13} options={mapOptions} onClick={select}>
    {position && <MarkerF position={position} title="Vehicle entrance" draggable={!disabled} onDragEnd={select} />}
  </GoogleMap>;
}

export default function EntranceLocationPicker({ position, onChange, disabled }: Props) {
  const mounted = useRef(false);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  const [locating, setLocating] = useState(false);
  const [message, setMessage] = useState('');
  const apiKey = import.meta.env.VITE_GOOGLE_MAPS_API_KEY;
  const locate = () => {
    setMessage('');
    if (!navigator.geolocation) { setMessage('Location is unavailable in this browser. Select a map point or enter coordinates.'); return; }
    setLocating(true);
    navigator.geolocation.getCurrentPosition(location => {
      if (!mounted.current) return;
      onChange({ lat: location.coords.latitude, lng: location.coords.longitude });
      setMessage(`Location found (estimated accuracy ${Math.round(location.coords.accuracy)} m). Adjust the pin to the vehicle entrance.`);
      setLocating(false);
    }, error => {
      if (!mounted.current) return;
      setMessage(error.code === 1 ? 'Location permission was denied. Select a map point or enter coordinates.'
        : 'Your location could not be found. Select a map point or enter coordinates.');
      setLocating(false);
    }, { enableHighAccuracy: true, timeout: 15000, maximumAge: 0 });
  };
  return <section className="zone-entrance-picker" aria-label="Parking entrance location">
    <div className="admin-toolbar"><h3>Vehicle entrance</h3><button type="button" className="btn btn-secondary" disabled={disabled || locating} onClick={locate}>{locating ? 'Finding location…' : 'Use my location'}</button></div>
    <p>Click the vehicle entrance on the map or drag the pin. Drivers will use this point to reach the parking zone.</p>
    {apiKey ? <EntranceMap position={position} onChange={onChange} disabled={disabled || locating} apiKey={apiKey} />
      : <p>The map is unavailable. Use your location or enter latitude and longitude below.</p>}
    <p role="status">{message || (position ? `Entrance: ${position.lat.toFixed(6)}, ${position.lng.toFixed(6)}` : 'No entrance selected yet.')}</p>
  </section>;
}
