import { useEffect, useMemo, useRef, useState } from 'react';
import { GoogleMap, MarkerF, useJsApiLoader } from '@react-google-maps/api';
import { LocateFixed, MapPin } from 'lucide-react';

export interface EntranceLocation { lat: number; lng: number }
interface Props { position: EntranceLocation | null; onChange: (position: EntranceLocation) => void; disabled: boolean }
const defaultCenter = { lat: 6.9271, lng: 79.8612 };
const mapStyle = { width: '100%', height: '300px' };
const mapOptions = { streetViewControl: false, fullscreenControl: false, clickableIcons: false };

function EntranceMap({ position, onChange, disabled, apiKey }: Props & { apiKey: string }) {
  const { isLoaded, loadError } = useJsApiLoader({ id: 'openparking-google-maps', googleMapsApiKey: apiKey });
  const lat = position?.lat, lng = position?.lng;
  const center = useMemo(() => lat !== undefined && lng !== undefined ? { lat, lng } : defaultCenter, [lat, lng]);
  const select = (event: google.maps.MapMouseEvent) => {
    if (!disabled && event.latLng) onChange({ lat: event.latLng.lat(), lng: event.latLng.lng() });
  };
  if (loadError) return <p role="status">The map could not load. Use your location or enter coordinates.</p>;
  if (!isLoaded) return <div className="skeleton" style={{ height: 300 }} role="status" aria-label="Loading entrance map" />;
  return <div className="entrance-map"><GoogleMap mapContainerStyle={mapStyle} center={center} zoom={position ? 18 : 13} options={mapOptions} onClick={select}>
    {position && <MarkerF position={position} title="Vehicle entrance" draggable={!disabled} onDragEnd={select} />}
  </GoogleMap></div>;
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
  return <section className="entrance-picker" aria-label="Parking entrance location">
    <div className="row-between"><h3>Vehicle entrance</h3><button type="button" className="btn btn-secondary btn-sm" disabled={disabled || locating} onClick={locate}><LocateFixed size={14} aria-hidden="true" />{locating ? 'Finding location…' : 'Use my location'}</button></div>
    <p>Click the entrance on the map or drag the pin. Drivers navigate to this point.</p>
    {apiKey ? <EntranceMap position={position} onChange={onChange} disabled={disabled || locating} apiKey={apiKey} />
      : <p>The map is unavailable. Use your location or enter latitude and longitude.</p>}
    <p role="status" className="row" style={{ gap: 6 }}><MapPin size={14} aria-hidden="true" />{message || (position ? `${position.lat.toFixed(6)}, ${position.lng.toFixed(6)}` : 'No entrance selected yet.')}</p>
  </section>;
}
