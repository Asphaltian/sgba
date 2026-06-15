using Sandbox.UI;

namespace Emulotl;

public sealed class TabRing : Panel
{
	private const float BorderWidth = 4f;
	private const float CornerRadius = 8f;
	private const float SampleStep = 1.2f;
	private const float AnimationDuration = 5.2f;
	private static readonly RingGradient Gradient = new(
		[0f, 0.14f, 0.28f, 0.45f, 0.62f, 0.78f, 1f],
		[
			new Color( 0.094f, 0.596f, 1f ),
			new Color( 0.188f, 0.651f, 1f ),
			new Color( 0.094f, 0.596f, 1f ),
			new Color( 0f, 0.365f, 1f ),
			new Color( 0.094f, 0.596f, 1f ),
			new Color( 0.188f, 0.651f, 1f ),
			new Color( 0.094f, 0.596f, 1f )
		] );

	public override void Tick()
	{
		base.Tick();
		MarkRenderDirty();
	}

	public override void OnDraw()
	{
		base.OnDraw();

		float width = Box.Rect.Width;
		float height = Box.Rect.Height;
		if ( width <= 0f || height <= 0f ) return;

		float scale = MathF.Max( 0.001f, ScaleToScreen );
		DrawRoundedRing( width, height, scale );
	}

	private void DrawRoundedRing( float width, float height, float scale )
	{
		float originX = Box.Rect.Left;
		float originY = Box.Rect.Top;
		float centerX = originX + width * 0.5f;
		float centerY = originY + height * 0.5f;
		float halfDot = BorderWidth * scale * 0.5f;
		float radius = CornerRadius * scale + halfDot;
		var center = new Vector2( centerX, centerY );
		float phase = RealTime.Now / AnimationDuration;

		float left = originX + halfDot;
		float top = originY + halfDot;
		float right = originX + width - halfDot;
		float bottom = originY + height - halfDot;

		var stroke = new RingStroke( Gradient, center, phase, halfDot, SampleStep * scale );
		stroke.Horizontal( left + radius, right - radius, top );
		stroke.Corner( right - radius, top + radius, radius, -90f, 0f );
		stroke.Vertical( right, top + radius, bottom - radius );
		stroke.Corner( right - radius, bottom - radius, radius, 0f, 90f );
		stroke.Horizontal( right - radius, left + radius, bottom );
		stroke.Corner( left + radius, bottom - radius, radius, 90f, 180f );
		stroke.Vertical( left, bottom - radius, top + radius );
		stroke.Corner( left + radius, top + radius, radius, 180f, 270f );
	}
}
