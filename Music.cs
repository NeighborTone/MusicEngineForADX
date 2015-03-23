//Copyright (c) 2014 geekdrums
//Released under the MIT license
//http://opensource.org/licenses/mit-license.php
//Feel free to use this for your lovely musical games :)

/* Note1: You must do this
Add this code in Plugins/CriWare/CriWare/CriAtomSource.cs
so that you can use SetFirstBlock function.
public CriAtomExPlayer Player
{
	get { return this.player; }
}
http://www53.atwiki.jp/soundtasukeai/pages/22.html#id_6c095b2d
*/

/* Note2: If you use many blocks in ADX2, you should do this
Auto update of block info.

Add this function in Editor/CriWare/CriAtom/CriAtomWindow.cs
...
using System.Xml;
...

	//(((((((((((((((geekdrums MusicEngine(((((((((((((((
	private void UpdateBlockInfo()
	{
		string sourceDirName = atomCraftOutputAssetsRootPath.Replace( "/Assets", "" );
		string[] files = System.IO.Directory.GetFiles( sourceDirName );
		foreach( string file in files )
		{
			if( file.EndsWith( "_acb_info.xml" ) )
			{
				string fileName = System.IO.Path.GetFileName( file );
				GameObject musicObj = GameObject.Find( fileName.Replace( "_acb_info.xml", "" ) );
				Music adxMusic = null;
				if( musicObj != null )
				{
					adxMusic = musicObj.GetComponent<Music>();
				}
				if( adxMusic != null )
				{
					adxMusic.BlockInfos.Clear();

					XmlReaderSettings settings = new XmlReaderSettings();
					settings.IgnoreWhitespace = true;
					settings.IgnoreComments = true;
					using( XmlReader reader = XmlReader.Create( System.IO.File.OpenText( file ), settings ) )
					{
						while( reader.Read() )
						{
							if( reader.GetAttribute( "Bpm" ) != null )
							{
								adxMusic.Tempo = double.Parse( reader.GetAttribute( "Bpm" ) );
							}
							if( adxMusic.Tempo > 0 && reader.GetAttribute( "BlockEndPositionMs" ) != null )
							{
								string blockName = reader.GetAttribute( "OrcaName" );
								int msec = int.Parse( reader.GetAttribute( "BlockEndPositionMs" ) );
								Music.BlockInfo blockInfo = new Music.BlockInfo( blockName, Mathf.RoundToInt( (msec / 1000.0f) / (4 * 60.0f / (float)adxMusic.Tempo) ) );
								adxMusic.BlockInfos.Add( blockInfo );
							}
						}
						reader.Close();
					}
				}
				//else there are no Music component for this AtomSource.
			}
			//else this file is not _acb_info.
		}
	}
	//(((((((((((((((geekdrums MusicEngine(((((((((((((((

and call this function from GUIImportAssetsFromAtomCraft() in same source code,
just after  CopyDirectory(atomCraftOutputAssetsRootPath, Application.dataPath); executed.
like this
				try
				{
					CopyDirectory(atomCraftOutputAssetsRootPath, Application.dataPath);
					Debug.Log("Complete Update Assets of \"CRI Atom Craft\"");

					//geekdrums MusicEngine
					UpdateBlockInfo();
				}

Preparation:
- Create a CriAtomSource object with Music component,
- specify mtBeat, mtBar and Tempo,
- name it with same name of CueName,
- then open CRI Atom window and push "Update Assets of CRI Atom Craft" button,
- your BlockInfo will be automatically updated.

*/

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(CriAtomSource))]
public class Music : MonoBehaviour
{
	#region BlockInfo class
	[System.Serializable]
	public class BlockInfo
	{
		public BlockInfo(string BlockName, int NumBar = 4)
		{
			this.BlockName = BlockName;
			this.NumBar = NumBar;
		}
		public string BlockName;
		public int NumBar = 4;
	}
	#endregion

	#region editor params
	/// <summary>
	/// how many MusicTime in a beat. maybe 4 or 3 or 6.
	/// </summary>
	public int UnitPerBeat = 4;
	/// <summary>
	/// how many MusicTime in a bar.
	/// </summary>
	public int UnitPerBar = 16;
	/// <summary>
	/// Musical Tempo. how many beats in a minutes.
	/// </summary>
	public double Tempo = 120;

	public List<BlockInfo> BlockInfos;

	/// <summary>
	/// put your debug TextMesh to see current musical time & block info.
	/// </summary>
	//public TextMesh DebugText;
	#endregion

	static Music Current_;
	static List<Music> MusicList = new List<Music>();
	static readonly int SamplingRate = 44100;

	#region public static properties
	public static bool IsPlaying { get { return Current_.IsPlaying_; } }
	/// <summary>
	/// means last timing.
	/// </summary>
	public static Timing Just { get { return Current_.Just_; } }
	/// <summary>
	/// means nearest timing.
	/// </summary>
	public static Timing Near { get { return Current_.Near_; } }
	/// <summary>
	/// is Just changed in this frame or not.
	/// </summary>
	public static bool IsJustChanged { get { return Current_.IsJustChanged_; } }
	/// <summary>
	/// is Near changed in this frame or not.
	/// </summary>
	public static bool IsNearChanged { get { return Current_.IsNearChanged_; } }
	/// <summary>
	/// is currently former half in a MusicTimeUnit, or last half.
	/// </summary>
	public static bool IsFormerHalf { get { return Current_.IsFormerHalf_; } }
	/// <summary>
	/// delta time from JustChanged.
	/// </summary>
	public static double TimeSecFromJust { get { return Current_.TimeSecFromJust_; } }
	/// <summary>
	/// how many times you repeat current music/block.
	/// </summary>
	public static int NumRepeat { get { return Current_.NumRepeat_; } }
	/// <summary>
	/// returns how long from nearest Just timing with sign.
	/// </summary>
	public static double lLg{ get{ return Current_.Lag_; } }
	/// <summary>
	/// returns how long from nearest Just timing absolutely.
	/// </summary>
	public static double LagAbs{ get{ return Current_.LagAbs_; } }
	/// <summary>
	/// returns normalized lag.
	/// </summary>
	public static double LagUnit{ get{ return Current_.LagUnit_; } }
	/// <summary>
	/// sec / musicalUnit
	/// </summary>
	public static double MusicalTimeUnit { get { return Current_.MusicalTimeUnit_; } }
	/// <summary>
	/// current musical time based on MusicalTimeUnit
	/// </summary>
	public static float MusicalTime { get { return Current_.MusicalTime_; } }
	/// <summary>
	/// dif from timing to now on musical time unit.
	/// </summary>
	/// <param name="timing"></param>
	/// <returns></returns>
	public static float MusicalTimeFrom(Timing timing)
	{
		return MusicalTime - timing.MusicalTime;
	}
	/// <summary>
	/// returns musically synced cos wave.
	/// if default( MusicalCos(16,0,0,1),
	/// starts from max=1,
	/// reaches min=0 on MusicalTime = cycle/2 = 8,
	/// back to max=1 on MusicalTIme = cycle = 16.
	/// </summary>
	/// <param name="cycle">wave cycle in musical unit</param>
	/// <param name="offset">wave offset in musical unit</param>
	/// <param name="min"></param>
	/// <param name="max"></param>
	/// <returns></returns>
	public static float MusicalCos(float cycle = 16, float offset = 0, float min = 0, float max = 1)
	{
		return Mathf.Lerp(min, max, ((float)Math.Cos(Math.PI * 2 * (MusicalTime + offset) / cycle) + 1.0f)/2.0f);
	}

	public static int CurrentUnitPerBar { get { return Current_.UnitPerBar; } }
	public static int CurrentUnitPerBeat { get { return Current_.UnitPerBeat; } }
	public static string CurrentMusicName { get { return Current_.name; } }
	public static string CurrentBlockName { get { return Current_.CurrentBlock_.BlockName; } }
	public static string NextBlockName { get { return Current_.NextBlock_.BlockName; } }
	public static CriAtomSource CurrentSource { get { return Current_.MusicSource_; } }
	public static BlockInfo CurrentBlock { get { return Current_.CurrentBlock_; } }
	public static BlockInfo NextBlock { get { return Current_.NextBlock_; } }
	#endregion

	#region public static predicates
	public static bool IsJustChangedWhen( Predicate<Timing> pred )
	{
		return Current_.IsJustChangedWhen_( pred );
	}
	public static bool IsJustChangedBar()
	{
		return Current_.IsJustChangedBar_();
	}
	public static bool IsJustChangedBeat()
	{
		return Current_.IsJustChangedBeat_();
	}
	public static bool IsJustChangedAt( int bar = 0, int beat = 0, int unit = 0 )
	{
		return Current_.IsJustChangedAt_( bar, beat, unit );
	}
	public static bool IsJustChangedAt( Timing t )
	{
		return Current_.IsJustChangedAt_( t.Bar, t.Beat, t.Unit );
	}

	public static bool IsNearChangedWhen( Predicate<Timing> pred )
	{
		return Current_.IsNearChangedWhen_( pred );
	}
	public static bool IsNearChangedBar()
	{
		return Current_.IsNearChangedBar_();
	}
	public static bool IsNearChangedBeat()
	{
		return Current_.IsNearChangedBeat_();
	}
	public static bool IsNearChangedAt( int bar, int beat = 0, int unit = 0 )
	{
		return Current_.IsNearChangedAt_( bar, beat, unit );
	}
	public static bool IsNearChangedAt( Timing t )
	{
		return Current_.IsNearChangedAt_( t.Bar, t.Beat, t.Unit );
	}
	#endregion

	#region public static functions
	/// <summary>
	/// Change Current Music.
	/// </summary>
	/// <param name="MusicName">name of the GameObject that include Music</param>
	public static void Play( string MusicName, string firstBlockName = "" ) { MusicList.Find( ( Music m ) => m.name == MusicName ).PlayStart( firstBlockName ); }
	/// <summary>
	/// Quantize to musical time( default is 16 beat ).
	/// </summary>
	/// <param name="source">your sound source( Unity AudioSource or ADX CriAtomSource )</param>
	public static void QuantizePlay(CriAtomSource source, int transpose = 0, float allowRange = 0.3f)
	{
		source.pitch = Mathf.Pow(PITCH_UNIT, transpose);
		if( IsFormerHalf && LagUnit < allowRange )
		{
			source.Play();
		}
		else
		{
			Current_.QuantizedCue_.Add(source);
		}
	}
	public static void Pause() { Current_.MusicSource_.Pause(true); }
	public static void Resume() { Current_.MusicSource_.Pause(false); }
	public static void Stop() { Current_.MusicSource_.Stop(); }
	public static void SetVolume(float volume)
	{
		Current_.MusicSource_.volume = volume;
	}

	//adx2 functions
	public static void SetNextBlock( string blockName )
	{
		if( blockName == CurrentBlockName ) return;
		Debug.Log( "SetNextBlock : " + blockName );
		int index = Current_.BlockInfos.FindIndex( ( BlockInfo info ) => info.BlockName == blockName );
		if( index >= 0 )
		{
			Current_.NextBlockIndex_ = index;
			Current_.Playback_.SetNextBlockIndex( index );
		}
		else
		{
			Debug.LogError( "Error!! Music.SetNextBlock Can't find block name: " + blockName );
		}
	}
	public static void SetNextBlock( int index )
	{
		if( index == Current_.CurrentBlockIndex_ ) return;
		if( index < Current_.CueInfo_.numBlocks )
		{
			Current_.NextBlockIndex_ = index;
			Current_.Playback_.SetNextBlockIndex( index );
		}
		else
		{
			Debug.LogError( "Error!! Music.SetNextBlock index is out of range: " + index );
		}
	}
	public static void SetFirstBlock( string blockName )
	{
		int index = Current_.BlockInfos.FindIndex( ( BlockInfo info ) => info.BlockName == blockName );
		if( index >= 0 )
		{
			Current_.NextBlockIndex_ = index;
			Current_.CurrentBlockIndex_ = index;
			Current_.MusicSource_.Player.SetFirstBlockIndex( index );
		}
		else
		{
			Debug.LogError( "Error!! Music.SetFirstBlock Can't find block name: " + blockName );
		}
	}
	public static void SetFirstBlock( int index )
	{
		if( index < Current_.CueInfo_.numBlocks )
		{
			Current_.NextBlockIndex_ = index;
			Current_.CurrentBlockIndex_ = index;
			Current_.MusicSource_.Player.SetFirstBlockIndex( index );
		}
		else
		{
			Debug.LogError( "Error!! Music.SetFirstBlock index is out of range: " + index );
		}
	}
	public static void SetAisac( uint index, float value )
	{
		Current_.MusicSource_.SetAisac( index, value );
	}
	public static void SetAisac( string controlName, float value )
	{
		Current_.MusicSource_.SetAisac( controlName, value );
	}
	#endregion

	#region private properties
	private Timing Near_;
	private Timing Just_;
	private bool IsJustChanged_;
	private bool IsNearChanged_;
	private bool IsFormerHalf_;
	private double TimeSecFromJust_;
	private int NumRepeat_;
	private double MusicalTimeUnit_;

	private double Lag_
	{
		get
		{
			if( IsFormerHalf_ )
				return TimeSecFromJust_;
			else
				return TimeSecFromJust_ - MusicalTimeUnit_;
		}
	}
	private double LagAbs_
	{
		get
		{
			if( IsFormerHalf_ )
				return TimeSecFromJust_;
			else
				return MusicalTimeUnit_ - TimeSecFromJust_;
		}
	}
	private double LagUnit_ { get { return Lag_ / MusicalTimeUnit_; } }
	private float MusicalTime_ { get { return (float)(Just_.MusicalTime + TimeSecFromJust_ / MusicalTimeUnit_); } }
	private float MusicalTimeBar_ { get { return MusicalTime_/UnitPerBar; } }
	private bool IsPlaying_ { get { return MusicSource_ != null && MusicSource_.status == CriAtomSource.Status.Playing; } }
	#endregion

	#region private predicates
	private bool IsNearChangedWhen_( Predicate<Timing> pred )
	{
		return IsNearChanged_ && pred( Near_ );
	}
	private bool IsNearChangedBar_()
	{
		return IsNearChanged_ && Near_.Beat == 0 && Near_.Unit == 0;
	}
	private bool IsNearChangedBeat_()
	{
		return IsNearChanged_ && Near_.Unit == 0;
	}
	private bool IsNearChangedAt_( int bar, int beat = 0, int unit = 0 )
	{
		return IsNearChanged_ &&
			Near_.Bar == bar && Near_.Beat == beat && Near_.Unit == unit;
	}
	private bool IsJustChangedWhen_( Predicate<Timing> pred )
	{
		return IsJustChanged_ && pred( Just_ );
	}
	private bool IsJustChangedBar_()
	{
		return IsJustChanged_ && Just_.Beat == 0 && Just_.Unit == 0;
	}
	private bool IsJustChangedBeat_()
	{
		return IsJustChanged_ && Just_.Unit == 0;
	}
	private bool IsJustChangedAt_( int bar = 0, int beat = 0, int unit = 0 )
	{
		return IsJustChanged_ &&
			Just_.Bar == bar && Just_.Beat == beat && Just_.Unit == unit;
	}
	#endregion

	#region private params
	CriAtomSource MusicSource_;
	CriAtomExPlayback Playback_;
	CriAtomExAcb ACBData_;
	CriAtomEx.CueInfo CueInfo_;
	List<CriAtomSource> QuantizedCue_ = new List<CriAtomSource>();
	int CurrentBlockIndex_;
	int CurrentSample_;
	/// <summary>
	/// you can't always get NextBlockIndex correctly, if ADX automatically change next block.
	/// </summary>
	int NextBlockIndex_;

	int SamplesPerUnit_;
	int SamplesPerBeat_;
	int SamplesPerBar_;

	Timing OldNear_, OldJust_;
	int OldBlockIndex_;
	int NumBlockBar_;
	bool PlayOnStart_ = false;

	BlockInfo CurrentBlock_ { get { return BlockInfos[CurrentBlockIndex_]; } }
	BlockInfo NextBlock_ { get { return BlockInfos[NextBlockIndex_]; } }
	int SamplesInLoop_ { get { return NumBlockBar_ * SamplesPerBar_; } }
	static readonly float PITCH_UNIT = Mathf.Pow(2.0f, 1.0f / 12.0f);
	#endregion

	#region Initialize & Update
	void Awake()
	{
		MusicList.Add( this );
        MusicSource_ = GetComponent<CriAtomSource>();
		if( Current_ == null && MusicSource_.playOnStart )
        {
			MusicSource_.playOnStart = false;
			PlayOnStart_ = true;
			Current_ = this;
        }
		ACBData_ = CriAtom.GetAcb( MusicSource_.cueSheet );
		ACBData_.GetCueInfo( MusicSource_.cueName, out CueInfo_ );

		double beatSec = (60.0 / Tempo);
		SamplesPerUnit_ = (int)(SamplingRate * (beatSec/UnitPerBeat));
		SamplesPerBeat_ =(int)(SamplingRate * beatSec);
		SamplesPerBar_ = (int)(SamplingRate * UnitPerBar * (beatSec/UnitPerBeat));
		MusicalTimeUnit_ = (double)SamplesPerUnit_ / (double)SamplingRate;

		Initialize();
	}

	void Initialize()
	{
		IsJustChanged_ = false;
		IsNearChanged_ = false;
		Near_ = new Timing( 0, 0, -1 );
		Just_ = new Timing( Near_ );
		OldNear_ = new Timing( Near_ );
		OldJust_ = new Timing( Just_ );
		TimeSecFromJust_ = 0;
		IsFormerHalf_ = true;
		NumRepeat_ = 0;
		CurrentBlockIndex_ = 0;
		OldBlockIndex_ = 0;
		NextBlockIndex_ = 0;
		CurrentSample_ = 0;
	}

	void PlayStart( string firstBlockName = "" )
	{
		if ( Current_ != null && IsPlaying )
		{
			Stop();
		}

		Current_ = this;
		Initialize();

		if ( firstBlockName != "" )
		{
			SetFirstBlock( firstBlockName );
		}
		Playback_ = MusicSource_.Play();
		NumBlockBar_ = BlockInfos[CurrentBlockIndex_].NumBar;
	}

	// Use this for initialization
	void Start()
	{
#if UNITY_EDITOR
		UnityEditor.EditorApplication.playmodeStateChanged = OnPlaymodeStateChanged;
#endif
		if( PlayOnStart_ )
		{
			PlayStart();
		}
	}

	// Update is called once per frame
	void Update()
	{
		if( IsPlaying )
		{
			UpdateTiming();
		}
	}
	
#if UNITY_EDITOR
	void OnPlaymodeStateChanged()
	{
		if( Current_.MusicSource_.Player != null )
		{
			if( UnityEditor.EditorApplication.isPaused )
			{
				Pause();
			}
			else
			{
				Resume();
			}
		}
	}
#endif

	void UpdateTiming()
	{
		int oldSample = CurrentSample_;
		long numSamples;
		int tempOut;
		if ( !Playback_.GetNumPlayedSamples( out numSamples, out tempOut ) )
		{
			numSamples = -1;
		}
		IsNearChanged_ = false;
		IsJustChanged_ = false;
		CurrentSample_ = (int)numSamples;
		if( CurrentSample_ >= 0 )
		{
			//BlockChanged
			if( CurrentSample_ < oldSample )
			{
				NumBlockBar_ = BlockInfos[Playback_.GetCurrentBlockIndex()].NumBar;
			}

			Just_.Bar = (int)(CurrentSample_ / SamplesPerBar_);
			Just_.Beat = (int)((CurrentSample_ - Just_.Bar * SamplesPerBar_) / SamplesPerBeat_);
			Just_.Unit = (int)((CurrentSample_ - Just_.Bar * SamplesPerBar_ - Just_.Beat * SamplesPerBeat_) / SamplesPerUnit_);
			if( NumBlockBar_ > 0 )
			{
				while( Just_.Bar >= NumBlockBar_ )
				{
					Just_--;
				}
			}

			TimeSecFromJust_ = (double)(CurrentSample_ - Just_.Bar * SamplesPerBar_ - Just_.Beat * SamplesPerBeat_ - Just_.Unit * SamplesPerUnit_) / (double)SamplingRate;
			IsFormerHalf_ = (TimeSecFromJust_ * SamplingRate) < SamplesPerUnit_ / 2;

			Near_.Copy(Just_);
			if( !IsFormerHalf_ )
			{
				Near_++;
				Near_.LoopBack(NumBlockBar_);
			}

			IsJustChanged_ = (Just_.Equals(OldJust_) == false);
			IsNearChanged_ = (Near_.Equals(OldNear_) == false);

			CallEvents();

			OldNear_.Copy(Near_);
			OldJust_.Copy(Just_);
		}

		/* DebugUpdateText
		if( DebugText != null )
		{
			DebugText.text = "Just = " + Just_.ToString() + ", MusicalTime = " + MusicalTime_;
			if( BlockInfos.Count > 0 )
			{
				DebugText.text += System.Environment.NewLine + "block[" + CurrentBlockIndex_ + "] = " + CurrentBlock_.BlockName + "(" + NumBlockBar_ + "bar)";
			}
		}
		*/
	}

	#endregion

	#region Events
	void CallEvents()
	{
		if( IsJustChanged_ ) OnJustChanged();
		if( IsJustChanged_ && Just_.Unit == 0 ) OnBeat();
		if( IsJustChanged_ && Just_.Beat == 0 && Just_.Unit == 0 ) OnBar();
		if( IsJustChanged_ && Just_ < OldJust_ )
		{
			CurrentBlockIndex_ = Playback_.GetCurrentBlockIndex();
			if( OldBlockIndex_ == CurrentBlockIndex_ )
			{
				OnBlockRepeated();
			}
			else
			{
				OnBlockChanged();
			}
			OldBlockIndex_ = CurrentBlockIndex_;
		}
	}

	//On events (when isJustChanged)
	void OnJustChanged()
	{
		foreach ( CriAtomSource cue in QuantizedCue_ )
		{
			cue.Play();
		}
		QuantizedCue_.Clear();
	}

	void OnBeat()
	{
	}

	void OnBar()
	{
	}

	void OnBlockRepeated()
	{
		++NumRepeat_;
	}

	void OnBlockChanged()
	{
		NumRepeat_ = 0;
	}
	#endregion
}

[Serializable]
public class Timing : IComparable<Timing>, IEquatable<Timing>
{
	public Timing(int bar = 0, int beat = 0, int unit = 0)
	{
		Bar = bar;
		Beat = beat;
		Unit = unit;
	}

	public Timing(Timing copy)
	{
		Copy(copy);
	}
	public Timing() { this.Init(); }
	public void Init() { Bar = 0; Beat = 0; Unit = 0; }
	public void Copy(Timing copy)
	{
		Bar = copy.Bar;
		Beat = copy.Beat;
		Unit = copy.Unit;
	}
	
	public int Bar, Beat, Unit;

	public int MusicalTime { get { return Bar * Music.CurrentUnitPerBar + Beat * Music.CurrentUnitPerBeat + Unit; } }
	public void Fix()
	{
		int totalUnit = Bar * Music.CurrentUnitPerBar + Beat * Music.CurrentUnitPerBeat + Unit;
		Bar = totalUnit / Music.CurrentUnitPerBar;
		Beat = (totalUnit - Bar*Music.CurrentUnitPerBar) / Music.CurrentUnitPerBeat;
		Unit = (totalUnit - Bar*Music.CurrentUnitPerBar - Beat * Music.CurrentUnitPerBeat);
	}
	public void Add(int bar, int beat = 0, int unit = 0)
	{
		Bar += bar;
		Beat += beat;
		Unit += unit;
		Fix();
	}
	public void Add(Timing t)
	{
		Bar += t.Bar;
		Beat += t.Beat;
		Unit += t.Unit;
		Fix();
	}
	public void Subtract(int bar, int beat = 0, int unit = 0)
	{
		Bar -= bar;
		Beat -= beat;
		Unit -= unit;
		Fix();
	}
	public void Subtract(Timing t)
	{
		Bar -= t.Bar;
		Beat -= t.Beat;
		Unit -= t.Unit;
		Fix();
	}
	public void LoopBack(int loopBar)
	{
		if( loopBar > 0 )
		{
			Bar += loopBar;
			Fix();
			Bar %= loopBar;
		}
	}

	public static bool operator >(Timing t, Timing t2) { return t.Bar > t2.Bar || (t.Bar == t2.Bar && t.Beat > t2.Beat) || (t.Bar == t2.Bar && t.Beat == t2.Beat && t.Unit > t2.Unit); }
	public static bool operator <(Timing t, Timing t2) { return !(t > t2) && !(t.Equals(t2)); }
	public static bool operator <=(Timing t, Timing t2) { return !(t > t2); }
	public static bool operator >=(Timing t, Timing t2) { return !(t < t2); }
	public static Timing operator ++(Timing t) { t.Unit++; t.Fix(); return t; }
	public static Timing operator --(Timing t) { t.Unit--; t.Fix(); return t; }

	public override bool Equals(object obj)
	{
		if( object.ReferenceEquals(obj, null) )
		{
			return false;
		}
		if( object.ReferenceEquals(obj, this) )
		{
			return true;
		}
		if( this.GetType() != obj.GetType() )
		{
			return false;
		}
		return this.Equals(obj as Timing);
	}

	public override int GetHashCode()
	{
		return base.GetHashCode();
	}

	public bool Equals(Timing other)
	{
		return (this.Bar == other.Bar && this.Beat == other.Beat && this.Unit == other.Unit);
	}

	public int CompareTo(Timing tother)
	{
		if( this.Equals(tother) ) return 0;
		else if( this > tother ) return 1;
		else return -1;
	}

	public override string ToString()
	{
		return Bar + " " + Beat + " " + Unit;
	}
}

