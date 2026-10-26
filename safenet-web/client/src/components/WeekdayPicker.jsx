const weekdays = ['Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday', 'Sunday'];

export default function WeekdayPicker({ selectedDays, onToggle, disabled = false }) {
  // Every day is a real toggle button: its selected state works with touch,
  // keyboard navigation, and screen readers, not just a change in color.
  return (
    <div className="day-picker" role="group" aria-label="Meeting days">
      {weekdays.map((day) => {
        const selected = selectedDays.includes(day);
        return (
          <button key={day} type="button" aria-label={day}
            aria-pressed={selected} className={selected ? 'selected' : ''}
            disabled={disabled} onClick={() => onToggle(day)}>
            <span className="day-check" aria-hidden="true">{selected ? '✓' : '+'}</span>
            {day.slice(0, 3)}
          </button>
        );
      })}
    </div>
  );
}
