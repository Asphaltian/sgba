using Sandbox.UI;

namespace sGBA;

internal readonly struct RingGradient
{
	private readonly float[] _stops;
	private readonly Color[] _colors;

	public RingGradient( float[] stops, Color[] colors )
	{
		_stops = stops;
		_colors = colors;
	}

	public Color Sample( float offset )
	{
		offset -= MathF.Floor( offset );

		for ( int i = 0; i < _stops.Length - 1; i++ )
		{
			if ( offset < _stops[i] || offset > _stops[i + 1] ) continue;

			float amount = (offset - _stops[i]) / (_stops[i + 1] - _stops[i]);
			return Lerp( _colors[i], _colors[i + 1], amount );
		}

		return _colors[0];
	}

	private static Color Lerp( Color from, Color to, float amount )
	{
		amount = Math.Clamp( amount, 0f, 1f );
		return new Color(
			from.r + (to.r - from.r) * amount,
			from.g + (to.g - from.g) * amount,
			from.b + (to.b - from.b) * amount,
			1f );
	}
}

internal readonly struct RingStroke
{
	private readonly RingGradient _gradient;
	private readonly Vector2 _center;
	private readonly float _phase;
	private readonly float _halfDot;
	private readonly float _sampleStep;
	private readonly float _alpha;

	public RingStroke( RingGradient gradient, Vector2 center, float phase, float halfDot, float sampleStep, float alpha = 1f )
	{
		_gradient = gradient;
		_center = center;
		_phase = phase;
		_halfDot = halfDot;
		_sampleStep = sampleStep;
		_alpha = alpha;
	}

	public void Horizontal( float startX, float endX, float pointY )
	{
		float direction = MathF.Sign( endX - startX );
		float distance = MathF.Abs( endX - startX );
		int steps = Math.Max( 1, (int)MathF.Ceiling( distance / _sampleStep ) );

		for ( int step = 0; step <= steps; step++ )
		{
			float pointX = startX + direction * MathF.Min( step * _sampleStep, distance );
			Dot( new Vector2( pointX, pointY ) );
		}
	}

	public void Vertical( float pointX, float startY, float endY )
	{
		float direction = MathF.Sign( endY - startY );
		float distance = MathF.Abs( endY - startY );
		int steps = Math.Max( 1, (int)MathF.Ceiling( distance / _sampleStep ) );

		for ( int step = 0; step <= steps; step++ )
		{
			float pointY = startY + direction * MathF.Min( step * _sampleStep, distance );
			Dot( new Vector2( pointX, pointY ) );
		}
	}

	public void Corner( float centerX, float centerY, float radius, float startDegrees, float endDegrees )
	{
		float arcLength = MathF.Abs( endDegrees - startDegrees ) * MathF.PI / 180f * radius;
		int steps = Math.Max( 1, (int)MathF.Ceiling( arcLength / _sampleStep ) );

		for ( int step = 0; step <= steps; step++ )
		{
			float amount = step / (float)steps;
			float degrees = startDegrees + (endDegrees - startDegrees) * amount;
			float radians = degrees * MathF.PI / 180f;
			Dot( new Vector2( centerX + MathF.Cos( radians ) * radius, centerY + MathF.Sin( radians ) * radius ) );
		}
	}

	public void FullCircle( float radius )
	{
		float circumference = MathF.PI * 2f * radius;
		int steps = Math.Max( 1, (int)MathF.Ceiling( circumference / _sampleStep ) );

		for ( int step = 0; step <= steps; step++ )
		{
			float amount = step / (float)steps;
			float radians = amount * MathF.PI * 2f;
			var point = new Vector2( _center.x + MathF.Cos( radians ) * radius, _center.y + MathF.Sin( radians ) * radius );
			Panel.Draw.Circle( point, _halfDot, WithAlpha( _gradient.Sample( amount + _phase ) ) );
		}
	}

	private void Dot( Vector2 point )
	{
		Panel.Draw.Circle( point, _halfDot, ColorAt( point ) );
	}

	private Color ColorAt( Vector2 point )
	{
		float angle = MathF.Atan2( point.y - _center.y, point.x - _center.x );
		float offset = (angle + MathF.PI) / (MathF.PI * 2f);
		return WithAlpha( _gradient.Sample( offset + _phase ) );
	}

	private Color WithAlpha( Color color )
	{
		return new Color( color.r, color.g, color.b, color.a * _alpha );
	}
}
