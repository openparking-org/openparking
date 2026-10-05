import { describe, expect, it } from 'vitest';
import { moveWithinFloor, resizeWithinFloor } from '../modules/space-availability/mappingGeometry';

describe('floor plan gestures', () => {
  it('keeps the whole parking space inside the floor when dragged past an edge', () => {
    expect(moveWithinFloor(1100, 650, 80, 55, true)).toEqual({ x: 920, y: 545 });
    expect(moveWithinFloor(-40, -20, 80, 55, false)).toEqual({ x: 0, y: 0 });
  });
  it('allows precise movement with snapping disabled', () => {
    expect(moveWithinFloor(123, 157, 80, 55, false)).toEqual({ x: 123, y: 157 });
    expect(moveWithinFloor(123, 157, 80, 55, true)).toEqual({ x: 120, y: 160 });
  });
  it('limits resizing to the remaining floor area and a usable minimum', () => {
    expect(resizeWithinFloor(900, 500, 500, 500, true)).toEqual({ width: 100, height: 100 });
    expect(resizeWithinFloor(40, 40, -10, 5, false)).toEqual({ width: 30, height: 30 });
  });
});
