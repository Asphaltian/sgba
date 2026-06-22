using Sandbox.Rendering;

namespace Emulotl.Nds;

public sealed partial class ComputeRenderer2D
{
	private ComputeShader _csLayerPre;
	private ComputeShader _csSpritePre;
	private ComputeShader _csSprite;
	private ComputeShader _csCompositor;
	private ComputeShader _csCapture;

	private int _slot;

	private void InitShaders()
	{
		_csLayerPre = new ComputeShader( "shaders/nds2d_layerpre.shader" );
		_csSpritePre = new ComputeShader( "shaders/nds2d_spritepre.shader" );
		_csSprite = new ComputeShader( "shaders/nds2d_sprite.shader" );
		_csCompositor = new ComputeShader( "shaders/nds2d_compositor.shader" );
		_csCapture = new ComputeShader( "shaders/nds2d_capture.shader" );
	}

	private void DisposeShaders()
	{
		_csLayerPre = null;
		_csSpritePre = null;
		_csSprite = null;
		_csCompositor = null;
		_csCapture = null;
	}

	private void UploadBuffers( int slot )
	{
		_vramBG.SetData<uint>( _snapVramBG[slot] );
		_vramOBJ.SetData<uint>( _snapVramOBJ[slot] );
		_palBG.SetData<uint>( _snapPalBG[slot] );
		_palOBJ.SetData<uint>( _snapPalOBJ[slot] );
		_bgConfigBuf.SetData<sBGConfig>( _snapLayer[slot] );
		_oamConfigBuf.SetData<sOAM>( _snapOAM[slot] );
		_rotscaleBuf.SetData<Vec4i>( _snapRot[slot] );
		_scanlineBuf.SetData<sScanline>( _snapScan[slot] );

		if ( _snapDispMode[slot] == 2 )
			_vramDisplay.SetData<uint>( _snapVramDisplay[slot] );
	}

	public bool UploadAndBuildCommandList()
	{
		if ( !GpuReady || RenderCommandList == null )
			return false;

		if ( !AcquireReadSlot( out int slot ) )
			return false;

		_slot = slot;
		UploadBuffers( slot );

		CommandList cmd = RenderCommandList;
		cmd.Reset();

		if ( _snapComp[slot].Enable3D != 0 && _render3D != null )
		{
			_render3D.BuildCommandList( cmd );
			if ( _output3D != null )
				cmd.UavBarrier( _output3D );
		}

		for ( int l = 0; l < 4; l++ )
			PrerenderLayer( cmd, l );

		PrerenderSprites( cmd );
		DoRenderSprites( cmd );
		RenderScreen( cmd );

		if ( _num == 0 && _snapCapOn[_slot] )
			DoCapture( cmd );

		return true;
	}

	private void DoCapture( CommandList cmd )
	{
		int s = _slot;
		int width = _snapCapWidth[s];
		int height = _snapCapHeight[s];
		int capsize = _snapCapSize[s];
		int dstblock = _snapCapBank[s];
		int dstoffset = _snapCapOffset[s];
		int layer = capsize == 0 ? ((dstblock << 2) | dstoffset) : dstblock;
		int bufferHeight = capsize == 0 ? 128 : 256;

		cmd.UavBarrier( _captureSrcA );

		if ( _snapCapSrcBEn[s] != 0 )
			_captureSrcB.SetData<uint>( _snapCapSrcB[s] );

		cmd.Attributes.Set( "SrcA2D", _captureSrcA );
		cmd.Attributes.Set( "SrcA3D", _output3D ?? _captureSrcA );
		cmd.Attributes.Set( "SrcB", _captureSrcB );
		cmd.Attributes.Set( "CaptureOut128", CaptureOutput128Tex );
		cmd.Attributes.Set( "CaptureOut256", CaptureOutput256Tex );
		cmd.Attributes.Set( "uSrcA3D", _snapCapSrcA3D[s] );
		cmd.Attributes.Set( "uSrcBEnable", _snapCapSrcBEn[s] );
		cmd.Attributes.Set( "uMode", _snapCapMode[s] );
		cmd.Attributes.Set( "uEVA", _snapCapEVA[s] );
		cmd.Attributes.Set( "uEVB", _snapCapEVB[s] );
		cmd.Attributes.Set( "uDstWidth", width );
		cmd.Attributes.Set( "uDstHeight", height );
		cmd.Attributes.Set( "uCapSize", capsize );
		cmd.Attributes.Set( "uDstLayer", layer );
		cmd.Attributes.Set( "uDstOffset", capsize == 0 ? 0 : dstoffset );
		cmd.Attributes.Set( "uBufferHeight", bufferHeight );
		cmd.Attributes.Set( "uScale", _scale );
		cmd.DispatchCompute( _csCapture, width * _scale, height * _scale, 1 );
		cmd.UavBarrier( capsize == 0 ? CaptureOutput128Tex : CaptureOutput256Tex );
	}

	private void PrerenderLayer( CommandList cmd, int layer )
	{
		sBGConfig bg = _snapLayer[_slot][layer];
		if ( bg.Type >= 6 || bg.SizeX <= 0 )
			return;

		cmd.Attributes.Set( "VramBG", _vramBG );
		cmd.Attributes.Set( "PalBG", _palBG );
		cmd.Attributes.Set( "BGConfig", _bgConfigBuf );
		cmd.Attributes.Set( "OutputLayer", _bgLayer[layer] );
		cmd.Attributes.Set( "VRAMMask", _bgVramMask );
		cmd.Attributes.Set( "CurBG", layer );
		cmd.Attributes.Set( "uScale", _scale );
		Texture cap128 = Cap128;
		if ( cap128 != null )
		{
			cmd.Attributes.Set( "CaptureOut128", cap128 );
			cmd.Attributes.Set( "CaptureOut256", Cap256 );
		}
		cmd.DispatchCompute( _csLayerPre, bg.SizeX, bg.SizeY, 1 );
		cmd.UavBarrier( _bgLayer[layer] );
	}

	private void PrerenderSprites( CommandList cmd )
	{
		cmd.Attributes.Set( "VramOBJ", _vramOBJ );
		cmd.Attributes.Set( "PalOBJ", _palOBJ );
		cmd.Attributes.Set( "OAMConfig", _oamConfigBuf );
		cmd.Attributes.Set( "OutputAtlas", _spriteAtlas );
		cmd.Attributes.Set( "VRAMMask", _objVramMask );
		cmd.Attributes.Set( "NumSprites", _snapNumSprites[_slot] );
		cmd.Attributes.Set( "uScale", _scale );
		Texture cap128 = Cap128;
		if ( cap128 != null )
		{
			cmd.Attributes.Set( "CaptureOut128", cap128 );
			cmd.Attributes.Set( "CaptureOut256", Cap256 );
		}
		cmd.DispatchCompute( _csSpritePre, AtlasW, AtlasH, 1 );
		cmd.UavBarrier( _spriteAtlas );
	}

	private void DoRenderSprites( CommandList cmd )
	{
		int ow = ScreenW * _scale;
		int oh = ScreenH * _scale;

		cmd.Attributes.Set( "SpriteAtlas", _spriteAtlas );
		cmd.Attributes.Set( "OAMConfig", _oamConfigBuf );
		cmd.Attributes.Set( "Rotscale", _rotscaleBuf );
		cmd.Attributes.Set( "OBJColor", _objColor );
		cmd.Attributes.Set( "OBJFlags", _objFlags );
		cmd.Attributes.Set( "NumSprites", _snapNumSprites[_slot] );
		cmd.Attributes.Set( "ScaleFactor", _scale );
		cmd.Attributes.Set( "WindowEnable", _snapWindowEnable[_slot] );
		cmd.Attributes.Set( "OutputW", ow );
		cmd.Attributes.Set( "OutputH", oh );
		cmd.DispatchCompute( _csSprite, ow, oh, 1 );
		cmd.UavBarrier( _objColor );
		cmd.UavBarrier( _objFlags );
	}

	private void RenderScreen( CommandList cmd )
	{
		int ow = ScreenW * _scale;
		int oh = ScreenH * _scale;
		sCompositorConfig comp = _snapComp[_slot];

		Texture target = (_snapScreenSwap[_slot] && _swapPeer != null) ? _swapPeer.OutputTexture : _output;
		Texture bg0 = (comp.Enable3D != 0 && _output3D != null) ? _output3D : _bgLayer[0];

		cmd.Attributes.Set( "CaptureSrcA", _captureSrcA );
		cmd.Attributes.Set( "BGLayer0", bg0 );
		cmd.Attributes.Set( "BGLayer1", _bgLayer[1] );
		cmd.Attributes.Set( "BGLayer2", _bgLayer[2] );
		cmd.Attributes.Set( "BGLayer3", _bgLayer[3] );
		cmd.Attributes.Set( "OBJColor", _objColor );
		cmd.Attributes.Set( "OBJFlags", _objFlags );
		cmd.Attributes.Set( "Scanlines", _scanlineBuf );
		cmd.Attributes.Set( "BGConfig", _bgConfigBuf );
		cmd.Attributes.Set( "MosaicTable", _mosaicBuf );
		cmd.Attributes.Set( "VramDisplay", _vramDisplay );
		cmd.Attributes.Set( "DispMode", _snapDispMode[_slot] );
		cmd.Attributes.Set( "VramCap", _snapVramCap[_slot] );
		if ( Cap256 != null )
			cmd.Attributes.Set( "CaptureOut256", Cap256 );
		cmd.Attributes.Set( "OutputTex", target );
		cmd.Attributes.Set( "ScaleFactor", _scale );
		cmd.Attributes.Set( "BGPrio0", comp.BGPrio0 );
		cmd.Attributes.Set( "BGPrio1", comp.BGPrio1 );
		cmd.Attributes.Set( "BGPrio2", comp.BGPrio2 );
		cmd.Attributes.Set( "BGPrio3", comp.BGPrio3 );
		cmd.Attributes.Set( "EnableOBJ", comp.EnableOBJ );
		cmd.Attributes.Set( "Enable3D", comp.Enable3D );
		cmd.Attributes.Set( "BlendCnt", comp.BlendCnt );
		cmd.Attributes.Set( "BlendEffect", comp.BlendEffect );
		cmd.Attributes.Set( "BlendCoef0", comp.BlendCoef0 );
		cmd.Attributes.Set( "BlendCoef1", comp.BlendCoef1 );
		cmd.Attributes.Set( "BlendCoef2", comp.BlendCoef2 );
		cmd.Attributes.Set( "OutputW", ow );
		cmd.Attributes.Set( "OutputH", oh );
		cmd.Attributes.Set( "BrightMode", _snapBrightMode[_slot] );
		cmd.Attributes.Set( "BrightFactor", _snapBrightFactor[_slot] );
		cmd.Attributes.Set( "ScreenOff", _snapScreenOff[_slot] );
		cmd.DispatchCompute( _csCompositor, ow, oh, 1 );
		cmd.UavBarrier( target );
	}
}
