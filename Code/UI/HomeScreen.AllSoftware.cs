using Sandbox.UI;

namespace Emulotl;

public partial class HomeScreen
{
	private Panel AllSoftwareScroll { get; set; }
	private Panel AllSoftwareScrollbar { get; set; }
	private Panel AllSoftwareScrollbarThumb { get; set; }
	private float _allSoftwareDesiredScrollY;
	private float _lastAllSoftwareScrollY = -1f;
	private int _allSoftwareTitleDirection = 1;
	private bool _pendingAllSoftwareScrollApply;
	private bool _allSoftwareSmoothScrollActive;
	private bool _allSoftwareScrollbarDragging;
	private float _allSoftwareScrollbarDragOffsetY;

	private const int AllSoftwareColumns = 6;
	private const float AllSoftwareCardWidth = 260f;
	private const float AllSoftwareCardHeight = 260f;
	private const float AllSoftwareColumnGap = 20f;
	private const float AllSoftwareRowGap = 20f;
	private const float AllSoftwareGridVerticalPadding = 54f;
	private const float AllSoftwareAddButtonHeight = 86f;
	private const float AllSoftwareAddButtonTopGap = 96f;
	private const float AllSoftwareAddButtonBottomGap = 72f;
	private const float AllSoftwareScrollViewportHeight = 906f;
	private const float AllSoftwareSelectionTopMargin = 16f;
	private const float AllSoftwareSelectionBottomMargin = 58f;
	private const float AllSoftwareSmoothScrollRate = 18f;
	private const float AllSoftwareScrollbarHeight = 906f;

	private readonly record struct AllSoftwareEntry( GameEntry Game, int Index, int Row, int Column, string Key );

	private bool ShowAllSoftwareSelection => ShowHomeSelection;
	private bool IsAllSoftwareAddSelected => ShowAllSoftwareSelection && _allSoftwareOpen && _input.UseGamepad && _allSoftwareSelection == AllSoftwareAddIndex;
	private int AllSoftwareAddIndex => _allGames.Count;
	private int AllSoftwareEntryCount => _allGames.Count;
	private int AllSoftwareGameRows => _allGames.Count == 0 ? 0 : (int)MathF.Ceiling( _allGames.Count / (float)AllSoftwareColumns );
	private int AllSoftwareAddRow => AllSoftwareGameRows;
	private float AllSoftwareRowStride => AllSoftwareCardHeight + AllSoftwareRowGap;
	private float AllSoftwareGridWidth => AllSoftwareColumns * AllSoftwareCardWidth + Math.Max( 0, AllSoftwareColumns - 1 ) * AllSoftwareColumnGap;
	private float AllSoftwareGameContentHeight => AllSoftwareGameRows == 0 ? 0f : AllSoftwareGameRows * AllSoftwareCardHeight + Math.Max( 0, AllSoftwareGameRows - 1 ) * AllSoftwareRowGap;
	private float AllSoftwareAddButtonTop => AllSoftwareGridVerticalPadding + AllSoftwareGameContentHeight + AllSoftwareAddButtonTopGap;
	private float AllSoftwareContentHeight => AllSoftwareAddButtonTop + AllSoftwareAddButtonHeight + AllSoftwareAddButtonBottomGap;
	private float AllSoftwareMaxScrollY => MathF.Max( 0f, AllSoftwareContentHeight - AllSoftwareScrollViewportHeight );
	private float AllSoftwareScrollScale => MathF.Max( 0.001f, AllSoftwareScroll?.ScaleToScreen ?? Panel?.ScaleToScreen ?? 1f );
	private float AllSoftwareScrollY => AllSoftwareScroll == null
		? _allSoftwareDesiredScrollY.Clamp( 0f, AllSoftwareMaxScrollY )
		: (AllSoftwareScroll.ScrollOffset.y / AllSoftwareScrollScale).Clamp( 0f, AllSoftwareMaxScrollY );
	private bool AllSoftwareShowsScrollbar => AllSoftwareContentHeight > AllSoftwareScrollViewportHeight;
	private float AllSoftwareScrollbarThumbHeight => !AllSoftwareShowsScrollbar ? AllSoftwareScrollbarHeight : MathF.Max( 72f, AllSoftwareScrollbarHeight * (AllSoftwareScrollViewportHeight / AllSoftwareContentHeight) );
	private float AllSoftwareScrollbarThumbTravel => MathF.Max( 0f, AllSoftwareScrollbarHeight - AllSoftwareScrollbarThumbHeight );
	private float AllSoftwareScrollbarThumbTop => AllSoftwareScrollbarThumbTravel <= 0f ? 0f : AllSoftwareScrollbarThumbTravel * (AllSoftwareScrollY / MathF.Max( 1f, AllSoftwareMaxScrollY ));

	private string AllSoftwareScrollbarThumbStyle
	{
		get
		{
			if ( !AllSoftwareShowsScrollbar )
				return "";

			return $"top: {AllSoftwareScrollbarThumbTop}px; height: {AllSoftwareScrollbarThumbHeight}px;";
		}
	}

	private string AllSoftwareGridStyle => $"width: {AllSoftwareGridWidth}px; height: {AllSoftwareContentHeight}px;";

	private string AllSoftwareGridItemStyle( int row, int column )
	{
		float left = column * (AllSoftwareCardWidth + AllSoftwareColumnGap);
		float top = AllSoftwareGridVerticalPadding + row * AllSoftwareRowStride;
		return $"left: {left}px; top: {top}px;";
	}

	private string AllSoftwareAddRowStyle => $"left: 0px; top: {AllSoftwareAddButtonTop}px; width: {AllSoftwareGridWidth}px;";

	private IEnumerable<AllSoftwareEntry> AllSoftwareGridItems
	{
		get
		{
			for ( int index = 0; index < AllSoftwareEntryCount; index++ )
			{
				int row = index / AllSoftwareColumns;
				int column = index % AllSoftwareColumns;
				yield return new AllSoftwareEntry( _allGames[index], index, row, column, _allGames[index].Path );
			}
		}
	}

	private IEnumerable<AllSoftwareEntry> AllSoftwareVisibleItems
	{
		get
		{
			int firstRow = Math.Max( 0, (int)MathF.Floor( (AllSoftwareScrollY - AllSoftwareGridVerticalPadding) / AllSoftwareRowStride ) - 1 );
			int visibleRows = Math.Max( 1, (int)MathF.Ceiling( AllSoftwareScrollViewportHeight / AllSoftwareRowStride ) + 2 );
			int first = firstRow * AllSoftwareColumns;
			int last = Math.Min( AllSoftwareEntryCount, first + AllSoftwareColumns * visibleRows );
			for ( int index = first; index < last; index++ )
			{
				int row = index / AllSoftwareColumns;
				int column = index % AllSoftwareColumns;
				yield return new AllSoftwareEntry( _allGames[index], index, row, column, _allGames[index].Path );
			}
		}
	}

	private void OpenAllSoftware()
	{
		var selectedGame = SelectedGame;
		_allSoftwareOpen = true;
		_navFocused = false;
		int allSoftwareIndex = selectedGame == null ? -1 : _allGames.IndexOf( selectedGame );
		_allSoftwareSelection = allSoftwareIndex >= 0 ? allSoftwareIndex : _allGames.Count == 0 ? AllSoftwareAddIndex : 0;
		EnsureAllSoftwareSelectionVisible();
		Sound.Play( "ui.button.press" );
		QueueArtworkRefresh();
		StateHasChanged();
	}

	private void CloseAllSoftware()
	{
		_allSoftwareOpen = false;
		_selectedIndex = HomeGameCount > 0 ? 0 : HomeViewMoreIndex;
		_navFocused = false;
		_homeCarouselSettleRemaining = 0f;
		_queuedHomeLaunchGame = null;

		SyncBackdropTexture();
		QueueArtworkRefresh();
		Sound.Play( "ui.popup.message.close" );
		StateHasChanged();
	}

	private void ActivateAllSoftwareItem( int index )
	{
		SelectAllSoftwareItem( index, _input.UseGamepad );
		if ( index == AllSoftwareAddIndex )
		{
			ActivateAllSoftwareAdd();
			return;
		}

		var game = index >= 0 && index < _allGames.Count ? _allGames[index] : null;
		if ( game != null )
			LaunchGame( game );
	}

	private void ActivateAllSoftwareItemWithMouse( int index )
	{
		SetAllSoftwareTitleDirectionForMouse( index );

		if ( _allSoftwareSelection != index )
		{
			SelectAllSoftwareItem( index, useGamepad: false );
			return;
		}

		_input.ForceMouseMode();
		var game = index >= 0 && index < _allGames.Count ? _allGames[index] : null;
		if ( game != null )
			LaunchGame( game );
	}

	private void SetAllSoftwareTitleDirectionForMouse( int index )
	{
		if ( index < 0 || index >= _allGames.Count )
			return;

		int row = index / AllSoftwareColumns;
		float itemCenterY = AllSoftwareGridVerticalPadding + row * AllSoftwareRowStride + AllSoftwareCardHeight * 0.5f - AllSoftwareScrollY;
		_allSoftwareTitleDirection = itemCenterY >= AllSoftwareScrollViewportHeight * 0.5f ? 1 : -1;
	}

	private void ActivateAllSoftwareAdd()
	{
		if ( _input.UseGamepad )
			SelectAllSoftwareItem( AllSoftwareAddIndex, useGamepad: true );
		else
			UseMouseInAllSoftware();

		OpenAddGamesModal( _input.UseGamepad );
	}

	private void SelectAllSoftwareItem( int index, bool useGamepad )
	{
		var next = index.Clamp( 0, AllSoftwareAddIndex );
		if ( _allSoftwareSelection == next )
		{
			if ( !useGamepad )
				UseMouseInAllSoftware();
			return;
		}

		_allSoftwareSelection = next;
		if ( useGamepad )
			SetGamepadMode();
		else
			_input.ForceMouseMode();

		EnsureAllSoftwareSelectionVisible( smooth: true );
		Sound.Play( "ui.button.over" );
		QueueArtworkRefresh();
		StateHasChanged();
	}

	private void NavigateAllSoftwareHorizontal( int direction )
	{
		if ( _allSoftwareSelection < 0 )
		{
			SelectFirstVisibleAllSoftwareItem( useGamepad: true );
			return;
		}

		int next = _allSoftwareSelection + direction;
		if ( next < 0 || next > AllSoftwareAddIndex )
			return;

		if ( _allSoftwareSelection == AllSoftwareAddIndex )
		{
			if ( direction < 0 && _allGames.Count > 0 )
				SelectAllSoftwareItem( _allGames.Count - 1, useGamepad: true );
			return;
		}

		if ( next == AllSoftwareAddIndex )
		{
			SelectAllSoftwareItem( AllSoftwareAddIndex, useGamepad: true );
			return;
		}

		int currentRow = _allSoftwareSelection / AllSoftwareColumns;
		int nextRow = next / AllSoftwareColumns;
		if ( currentRow != nextRow )
			return;

		SelectAllSoftwareItem( next, useGamepad: true );
	}

	private void NavigateAllSoftwareVertical( int direction )
	{
		_allSoftwareTitleDirection = Math.Sign( direction );

		if ( _allSoftwareSelection < 0 )
		{
			SelectFirstVisibleAllSoftwareItem( useGamepad: true );
			return;
		}

		if ( direction > 0 && _allSoftwareSelection >= Math.Max( 0, _allGames.Count - AllSoftwareColumns ) )
		{
			SelectAllSoftwareItem( AllSoftwareAddIndex, useGamepad: true );
			return;
		}

		if ( direction < 0 && _allSoftwareSelection == AllSoftwareAddIndex )
		{
			int rowStart = Math.Max( 0, (AllSoftwareGameRows - 1) * AllSoftwareColumns );
			int column = AllSoftwareColumns / 2;
			SelectAllSoftwareItem( Math.Min( Math.Max( 0, _allGames.Count - 1 ), rowStart + column ), useGamepad: true );
			return;
		}

		int next = _allSoftwareSelection + direction * AllSoftwareColumns;
		if ( next < 0 || next >= _allGames.Count )
			return;

		SelectAllSoftwareItem( next, useGamepad: true );
	}

	private void EnsureAllSoftwareSelectionVisible( bool smooth = false )
	{
		if ( _allSoftwareSelection < 0 )
		{
			return;
		}

		bool isAddSelected = IsAllSoftwareAddSelected;
		int row = isAddSelected ? AllSoftwareAddRow : _allSoftwareSelection / AllSoftwareColumns;
		float itemBaseTop = isAddSelected ? AllSoftwareAddButtonTop : AllSoftwareGridVerticalPadding + row * AllSoftwareRowStride;
		float itemHeight = isAddSelected ? AllSoftwareAddButtonHeight : AllSoftwareCardHeight;
		float itemTop = !isAddSelected && row == 0 ? 0f : MathF.Max( 0f, itemBaseTop - AllSoftwareSelectionTopMargin );
		float itemBottom = itemBaseTop + itemHeight + AllSoftwareSelectionBottomMargin;
		float viewTop = AllSoftwareScrollY;
		float viewBottom = viewTop + AllSoftwareScrollViewportHeight;
		float nextScroll = viewTop;

		if ( isAddSelected )
			nextScroll = AllSoftwareMaxScrollY;
		else if ( itemTop < viewTop )
			nextScroll = itemTop;
		else if ( itemBottom > viewBottom )
			nextScroll = itemBottom - AllSoftwareScrollViewportHeight;

		SetAllSoftwareScrollY( nextScroll, smooth );
	}

	private void SetAllSoftwareScrollY( float scrollY, bool smooth = false )
	{
		_allSoftwareDesiredScrollY = scrollY.Clamp( 0f, AllSoftwareMaxScrollY );
		_allSoftwareSmoothScrollActive = smooth;
		_pendingAllSoftwareScrollApply = !smooth;

		if ( AllSoftwareScroll == null )
			return;

		if ( smooth )
			return;

		AllSoftwareScroll.ScrollOffset = new Vector2( 0f, _allSoftwareDesiredScrollY * AllSoftwareScrollScale );
		AllSoftwareScroll.ScrollVelocity = Vector2.Zero;
		_lastAllSoftwareScrollY = _allSoftwareDesiredScrollY;
		_pendingAllSoftwareScrollApply = false;
	}

	private void UpdateAllSoftwareScrollState()
	{
		if ( !_allSoftwareOpen || AllSoftwareScroll == null )
			return;

		UpdateAllSoftwareScrollbarDrag();

		if ( _allSoftwareSmoothScrollActive )
		{
			float current = AllSoftwareScrollY;
			float next = current.LerpTo( _allSoftwareDesiredScrollY, Time.Delta * AllSoftwareSmoothScrollRate );
			if ( MathF.Abs( next - _allSoftwareDesiredScrollY ) < 0.5f )
			{
				next = _allSoftwareDesiredScrollY;
				_allSoftwareSmoothScrollActive = false;
			}

			AllSoftwareScroll.ScrollOffset = new Vector2( 0f, next * AllSoftwareScrollScale );
			AllSoftwareScroll.ScrollVelocity = Vector2.Zero;
			_lastAllSoftwareScrollY = next;
			QueueArtworkRefresh();
			StateHasChanged();
			return;
		}

		if ( _pendingAllSoftwareScrollApply )
		{
			SetAllSoftwareScrollY( _allSoftwareDesiredScrollY );
			QueueArtworkRefresh();
			StateHasChanged();
			return;
		}

		float scrollY = AllSoftwareScrollY;
		if ( MathF.Abs( scrollY - _lastAllSoftwareScrollY ) < 0.5f )
			return;

		_allSoftwareTitleDirection = Math.Sign( scrollY - _lastAllSoftwareScrollY );
		_allSoftwareDesiredScrollY = scrollY;
		_lastAllSoftwareScrollY = scrollY;
		ClearAllSoftwareSelectionForMouseScroll();
		QueueArtworkRefresh();
		StateHasChanged();
	}

	private void OnAllSoftwareScrollbarMouseDown()
	{
		if ( !AllSoftwareShowsScrollbar || AllSoftwareScrollbar == null )
			return;

		UseMouseInAllSoftware();
		_allSoftwareSmoothScrollActive = false;
		_allSoftwareScrollbarDragging = true;

		float thumbTop = AllSoftwareScrollbarThumb?.Box.Top ?? 0f;
		float thumbBottom = AllSoftwareScrollbarThumb?.Box.Bottom ?? 0f;
		float thumbHeight = MathF.Max( 1f, thumbBottom - thumbTop );
		bool pressedThumb = Mouse.Position.y >= thumbTop && Mouse.Position.y <= thumbBottom;
		_allSoftwareScrollbarDragOffsetY = pressedThumb ? Mouse.Position.y - thumbTop : thumbHeight * 0.5f;

		UpdateAllSoftwareScrollbarDrag();
	}

	protected override void OnMouseMove( MousePanelEvent e )
	{
		base.OnMouseMove( e );

		if ( !_allSoftwareScrollbarDragging )
			return;

		UpdateAllSoftwareScrollbarDrag();
		e.StopPropagation();
	}

	protected override void OnMouseUp( MousePanelEvent e )
	{
		base.OnMouseUp( e );

		if ( !_allSoftwareScrollbarDragging )
			return;

		_allSoftwareScrollbarDragging = false;
		e.StopPropagation();
	}

	private void UpdateAllSoftwareScrollbarDrag()
	{
		if ( !_allSoftwareScrollbarDragging || AllSoftwareScrollbar == null || AllSoftwareScroll == null || !AllSoftwareShowsScrollbar )
			return;

		float trackHeight = MathF.Max( 1f, AllSoftwareScrollbar.Box.Rect.Height );
		float thumbHeight = MathF.Max( 1f, AllSoftwareScrollbarThumbHeight * AllSoftwareScrollScale );
		float travel = MathF.Max( 1f, trackHeight - thumbHeight );
		float thumbTop = (Mouse.Position.y - AllSoftwareScrollbar.Box.Rect.Top - _allSoftwareScrollbarDragOffsetY).Clamp( 0f, travel );
		float scrollY = (thumbTop / travel) * AllSoftwareMaxScrollY;

		SetAllSoftwareScrollY( scrollY );
		QueueArtworkRefresh();
		StateHasChanged();
	}

	private bool IsAllSoftwareSelected( int index )
	{
		return ShowAllSoftwareSelection && _allSoftwareOpen && _allSoftwareSelection == index;
	}

	private bool ShouldShowAllSoftwareTitleOnTop( int index )
	{
		if ( index < 0 || index >= _allGames.Count || AllSoftwareGameRows <= 1 )
			return false;

		int row = index / AllSoftwareColumns;
		if ( row == 0 )
			return false;

		if ( row == AllSoftwareGameRows - 1 )
			return true;

		return _allSoftwareTitleDirection > 0;
	}

	private string AllSoftwareAddButtonClass => "all-add-button" + (IsAllSoftwareAddSelected ? " selected" : "");

	private void UseMouseInAllSoftware()
	{
		_input.ForceMouseMode();
		_allSoftwareSelection = -1;
		StateHasChanged();
	}

	private void ClearAllSoftwareSelectionForMouseScroll()
	{
		_input.ForceMouseMode();
		_allSoftwareSelection = -1;
	}

	private void SelectFirstVisibleAllSoftwareItem( bool useGamepad )
	{
		if ( _allGames.Count == 0 )
		{
			SelectAllSoftwareItem( AllSoftwareAddIndex, useGamepad );
			return;
		}

		int firstVisibleRow = Math.Max( 0, (int)MathF.Floor( (AllSoftwareScrollY - AllSoftwareGridVerticalPadding) / AllSoftwareRowStride ) );
		int firstVisible = (firstVisibleRow * AllSoftwareColumns).Clamp( 0, _allGames.Count - 1 );
		SelectAllSoftwareItem( firstVisible, useGamepad );
	}
}
