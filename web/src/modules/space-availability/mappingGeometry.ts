export function moveWithinFloor(x: number, y: number, width: number, height: number, snap: boolean) {
  const round = (value: number) => snap ? Math.round(value / 10) * 10 : Math.round(value);
  return { x: Math.max(0, Math.min(1000 - width, round(x))), y: Math.max(0, Math.min(600 - height, round(y))) };
}

export function resizeWithinFloor(x: number, y: number, width: number, height: number, snap: boolean) {
  const round = (value: number) => snap ? Math.round(value / 10) * 10 : Math.round(value);
  return { width: Math.max(30, Math.min(1000 - x, round(width))), height: Math.max(30, Math.min(600 - y, round(height))) };
}
