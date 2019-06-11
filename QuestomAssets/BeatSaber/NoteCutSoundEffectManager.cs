using QuestomAssets.AssetsChanger;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace QuestomAssets.BeatSaber
{
    public class NoteCutSoundEffectManager : MonoBehaviourObject, INeedAssetsMetadata
    {
        public List<ISmartPtr<AudioClipObject>> DefaultLongCutEffectsAudioClips { get; private set; }

        public ISmartPtr<MonoBehaviourObject> gameDidPauseSignal { get; set; }
        public ISmartPtr<MonoBehaviourObject> gameDidResumeSignal { get; set; }
        public ISmartPtr<MonoBehaviourObject> audioManager { get; set; }
        public float audioSamplesBeatAlignOffset { get; set; }
        public int maxNumberOfEffects { get; set; }
        public List<ISmartPtr<AudioClipObject>> longCutEffectsAudioClips { get; set; }
        public List<ISmartPtr<AudioClipObject>> shortCutEffectsAudioClips { get; set; }
        public ISmartPtr<AudioClipObject> testAudioClip { get; set; }

        public void Reset(AssetsManager manager)
        {
            longCutEffectsAudioClips.Clear();
            shortCutEffectsAudioClips.Clear();
            // Find all the required audio clips and make pointers to them from this object.
            longCutEffectsAudioClips.AddRange(manager.MassFindAssets<AudioClipObject>(a => a.Object.Name.Contains("HitLong"))
                .Select(a => a.PtrFrom(this)));
            shortCutEffectsAudioClips.AddRange(manager.MassFindAssets<AudioClipObject>(a => a.Object.Name.Contains("HitShort"))
                .Select(a => a.PtrFrom(this)));
        }

        public NoteCutSoundEffectManager(AssetsFile assetsFile) : base(assetsFile, assetsFile.Manager.GetScriptObject("NoteCutSoundEffectManager"))
        {
        }

        public NoteCutSoundEffectManager(IObjectInfo<AssetsObject> objectInfo, AssetsReader reader) : base(objectInfo)
        {
            Parse(reader);
        }

        protected override void Parse(AssetsReader reader)
        {
            base.Parse(reader);
            gameDidPauseSignal = SmartPtr<MonoBehaviourObject>.Read(ObjectInfo.ParentFile, this, reader);
            gameDidResumeSignal = SmartPtr<MonoBehaviourObject>.Read(ObjectInfo.ParentFile, this, reader);
            audioManager = SmartPtr<MonoBehaviourObject>.Read(ObjectInfo.ParentFile, this, reader);
            audioSamplesBeatAlignOffset = reader.ReadSingle();
            maxNumberOfEffects = reader.ReadInt32();
            longCutEffectsAudioClips = reader.ReadArrayOf<ISmartPtr<AudioClipObject>>(a => SmartPtr<AudioClipObject>.Read(ObjectInfo.ParentFile, this, reader));
            shortCutEffectsAudioClips = reader.ReadArrayOf<ISmartPtr<AudioClipObject>>(a => SmartPtr<AudioClipObject>.Read(ObjectInfo.ParentFile, this, reader));
            testAudioClip = SmartPtr<AudioClipObject>.Read(ObjectInfo.ParentFile, this, reader);
        }

        public override void Write(AssetsWriter writer)
        {
            base.WriteBase(writer);
            gameDidPauseSignal.Write(writer);
            gameDidResumeSignal.Write(writer);
            audioManager.Write(writer);
            writer.Write(audioSamplesBeatAlignOffset);
            writer.Write(maxNumberOfEffects);
            writer.WriteArrayOf(longCutEffectsAudioClips, (a, w) => a.Write(w));
            writer.WriteArrayOf(shortCutEffectsAudioClips, (a, w) => a.Write(w));
            testAudioClip.Write(writer);
        }
    }
}
