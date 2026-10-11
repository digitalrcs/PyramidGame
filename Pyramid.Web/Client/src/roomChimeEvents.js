const strings = values => [...new Set((Array.isArray(values) ? values : []).filter(v => typeof v === 'string'))];

// Compare observations, never transcript text. Room details stay visibility-scoped;
// personal score/puzzle totals supply reward cues without revealing remote puzzles.
export function roomObservation(state, party, userId, active) {
  if (!state) return null;
  const visible = state.roomChanges?.visible ?? state.roomLit ?? true;
  const companions = (party?.members || []).filter(p => p.id !== userId && p.here)
    .map(p => ({ id: p.id, name: p.name || 'An explorer' }));
  const characters = visible ? (Array.isArray(state.characters) ? state.characters : [])
    .map(p => typeof p === 'string' ? { id: p, name: p } : p)
    .filter(p => p && typeof p.id === 'string').map(p => ({ id: p.id, name: p.name || 'A figure' })) : [];
  return {
    active, world: `${userId || ''}:${party?.id || 'solo'}:${state.packId}`,
    score: Number.isFinite(state.score) ? state.score : null,
    puzzleCount: Number.isFinite(state.solved) ? state.solved : null,
    moves: Number.isFinite(state.moves) ? state.moves : null,
    started: state.started, won: !!state.won,
    room: `${state.level}:${state.roomId}`, visible, activity: !!state.roomActivity,
    items: visible ? strings(state.items) : [],
    routes: visible ? strings(state.roomChanges?.openRoutes) : [],
    solved: visible ? strings(state.roomChanges?.solved) : [], companions, characters,
  };
}

const added = (before, after) => after.filter(value => !before.includes(value));
export function roomEvents(previous, next) {
  if (!next?.active) return [];
  // Switching an account or adventure is a new baseline, not a room event.
  if (previous && previous.world !== next.world) return [];
  if (!previous?.active)
    return next.activity ? [{ kind: 'activity', text: 'Something here invites investigation.' }] : [];
  // A reset/import must establish a baseline rather than sound like earned rewards.
  if ((previous.started && next.started && previous.started !== next.started)
      || (previous.moves !== null && next.moves !== null && next.moves < previous.moves)
      || (previous.puzzleCount !== null && next.puzzleCount !== null && next.puzzleCount < previous.puzzleCount)
      || (previous.won && !next.won)) return [];
  const events = [];
  const puzzleSolved = previous.puzzleCount !== null && next.puzzleCount !== null && next.puzzleCount > previous.puzzleCount;
  const points = previous.score !== null && next.score !== null ? next.score - previous.score : 0;
  if (!previous.won && next.won) events.push({ kind: 'victory', text: 'Adventure complete!' });
  if (puzzleSolved) events.push({ kind: 'puzzle', text: 'Puzzle solved.' });
  if (points > 0) events.push({ kind: 'reward', text: `You gained ${points} ${points === 1 ? 'point' : 'points'}.` });
  // Movement can earn exploration points, but occupants of the new room are a baseline.
  if (previous.room !== next.room) {
    if (next.activity) events.push({ kind: 'activity', text: 'Something here invites investigation.' });
    return events;
  }
  const changes = [];
  if (!previous.visible && next.visible) changes.push('The room is now visible.');
  else if (next.visible) {
    if (added(previous.items, next.items).length) changes.push('An item has appeared here.');
    if (added(previous.routes, next.routes).length) changes.push('A new route has opened.');
    if (!puzzleSolved && added(previous.solved, next.solved).length)
      events.push({ kind: 'puzzle', text: 'A puzzle in this room has been solved.' });
  }
  if (changes.length) events.push({ kind: 'activity', text: changes.join(' ') });
  const arrivals = next.companions.filter(p => !previous.companions.some(old => old.id === p.id));
  // Revealing existing figures with a lamp is already covered by the light cue.
  if (previous.visible && next.visible)
    arrivals.push(...next.characters.filter(p => !previous.characters.some(old => old.id === p.id)));
  if (arrivals.length) events.push({ kind: 'arrival', text: `${arrivals.map(p => p.name).join(', ')} ${arrivals.length === 1 ? 'has' : 'have'} entered the room.` });
  return events;
}

// One motif per observation even when solving a puzzle awards points and opens a route.
export function roomSound(events) {
  return ['victory', 'puzzle', 'reward', 'arrival', 'activity'].find(kind => events.some(event => event.kind === kind)) || null;
}
