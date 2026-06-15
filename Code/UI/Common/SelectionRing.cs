using Sandbox.UI;

namespace Emulotl;

public sealed class SelectionRing : Panel
{
	private const float RoundedStrokeWidth = 10f;
	private const float CirclePadding = 3f;
	private const float RoundedGap = 5f;
	private const float RoundedCornerRadius = 17f;
	private const float SampleStep = 2f;
	private const float AnimationDuration = 5.2f;
	private static readonly RingGradient Gradient = new(
		[0f, 0.14f, 0.28f, 0.45f, 0.62f, 0.78f, 1f],
		[
			new Color( 0.075f, 0.592f, 1f ),
			new Color( 0.373f, 0.82f, 1f ),
			new Color( 0.075f, 0.592f, 1f ),
			new Color( 0f, 0.357f, 1f ),
			new Color( 0.075f, 0.592f, 1f ),
			new Color( 0.373f, 0.82f, 1f ),
			new Color( 0.075f, 0.592f, 1f )
		] );

	[Parameter]
	public bool Active { get; set; } = true;

	[Parameter]
	public bool Circle { get; set; }

	[Parameter]
	public float StrokeWidth { get; set; } = RoundedStrokeWidth;

	[Parameter]
	public float CornerRadius { get; set; } = RoundedCornerRadius;

	[Parameter]
	public float Gap { get; set; } = RoundedGap;

	[Parameter]
	public bool Outset { get; set; }

	[Parameter]
	public bool FlatTop { get; set; }

	[Parameter]
	public float Alpha { get; set; } = 1f;

	public override void Tick()
	{
		base.Tick();
		MarkRenderDirty();
	}

	public override void OnDraw()
	{
		base.OnDraw();

		if ( !Active ) return;

		float width = Box.Rect.Width;
		float height = Box.Rect.Height;
		if ( width <= 0f || height <= 0f ) return;
		float alpha = Math.Clamp( Alpha, 0f, 1f );
		if ( alpha <= 0f ) return;

		float scale = MathF.Max( 0.001f, ScaleToScreen );
		if ( Circle )
		{
			DrawCircleRing( width, height, scale, alpha );
			return;
		}

		if ( FlatTop )
		{
			DrawFlatTopRing( width, height, scale, alpha );
			return;
		}

		DrawRoundedRing( width, height, scale, alpha );
	}

	private void DrawFlatTopRing( float width, float height, float scale, float alpha )
	{
		float originX = Box.Rect.Left;
		float originY = Box.Rect.Top;
		float centerX = originX + width * 0.5f;
		float centerY = originY + height * 0.5f;
		float halfDot = MathF.Max( 1f, StrokeWidth ) * scale * 0.5f;
		float centerOffset = Outset ? halfDot + MathF.Max( 0f, Gap ) * scale : halfDot;
		float radius = Outset
			? MathF.Max( 0f, CornerRadius ) * scale + centerOffset
			: (MathF.Max( 0f, CornerRadius ) + MathF.Max( 0f, Gap )) * scale;
		var center = new Vector2( centerX, centerY );
		float phase = RealTime.Now / AnimationDuration;

		float left = Outset ? originX - centerOffset : originX + centerOffset;
		float top = Outset ? originY - centerOffset : originY + centerOffset;
		float right = Outset ? originX + width + centerOffset : originX + width - centerOffset;
		float bottom = Outset ? originY + height + centerOffset : originY + height - centerOffset;
		float bottomRadius = MathF.Min( radius, MathF.Min( MathF.Abs( right - left ) * 0.5f, MathF.Abs( bottom - top ) ) );

		var stroke = new RingStroke( Gradient, center, phase, halfDot, SampleStep * scale, alpha );
		stroke.Horizontal( left, right, top );
		stroke.Vertical( right, top, bottom - bottomRadius );
		stroke.Corner( right - bottomRadius, bottom - bottomRadius, bottomRadius, 0f, 90f );
		stroke.Horizontal( right - bottomRadius, left + bottomRadius, bottom );
		stroke.Corner( left + bottomRadius, bottom - bottomRadius, bottomRadius, 90f, 180f );
		stroke.Vertical( left, bottom - bottomRadius, top );
	}

	private void DrawRoundedRing( float width, float height, float scale, float alpha )
	{
		float originX = Box.Rect.Left;
		float originY = Box.Rect.Top;
		float centerX = originX + width * 0.5f;
		float centerY = originY + height * 0.5f;
		float halfDot = MathF.Max( 1f, StrokeWidth ) * scale * 0.5f;
		float centerOffset = Outset ? halfDot + MathF.Max( 0f, Gap ) * scale : halfDot;
		float radius = Outset
			? MathF.Max( 0f, CornerRadius ) * scale + centerOffset
			: (MathF.Max( 0f, CornerRadius ) + MathF.Max( 0f, Gap )) * scale;
		var center = new Vector2( centerX, centerY );
		float phase = RealTime.Now / AnimationDuration;

		float left = Outset ? originX - centerOffset : originX + centerOffset;
		float top = Outset ? originY - centerOffset : originY + centerOffset;
		float right = Outset ? originX + width + centerOffset : originX + width - centerOffset;
		float bottom = Outset ? originY + height + centerOffset : originY + height - centerOffset;

		var stroke = new RingStroke( Gradient, center, phase, halfDot, SampleStep * scale, alpha );
		stroke.Horizontal( left + radius, right - radius, top );
		stroke.Corner( right - radius, top + radius, radius, -90f, 0f );
		stroke.Vertical( right, top + radius, bottom - radius );
		stroke.Corner( right - radius, bottom - radius, radius, 0f, 90f );
		stroke.Horizontal( right - radius, left + radius, bottom );
		stroke.Corner( left + radius, bottom - radius, radius, 90f, 180f );
		stroke.Vertical( left, bottom - radius, top + radius );
		stroke.Corner( left + radius, top + radius, radius, 180f, 270f );
	}

	private void DrawCircleRing( float width, float height, float scale, float alpha )
	{
		float halfDot = MathF.Max( 1f, StrokeWidth ) * scale * 0.5f;
		var center = new Vector2( Box.Rect.Left + width * 0.5f, Box.Rect.Top + height * 0.5f );
		float radius = Outset
			? MathF.Min( width, height ) * 0.5f + MathF.Max( 0f, Gap ) * scale + halfDot
			: MathF.Min( width, height ) * 0.5f - halfDot - CirclePadding * scale;
		float phase = RealTime.Now / AnimationDuration;

		new RingStroke( Gradient, center, phase, halfDot, SampleStep * scale, alpha ).FullCircle( radius );
	}

	protected override int BuildHash()
	{
		return HashCode.Combine( Active, Circle, StrokeWidth, CornerRadius, Gap, Outset, FlatTop, Alpha );
	}
}
