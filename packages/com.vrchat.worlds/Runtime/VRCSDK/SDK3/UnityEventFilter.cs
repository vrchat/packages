using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Components;
using TMPro;
#endif
#if UDON
using VRC.Udon;

#endif
#if !VRC_CLIENT && UNITY_EDITOR && VRC_SDK_VRCSDK3
using UnityEditor;
using UnityEngine.SceneManagement;
#endif

namespace VRC.Core
{
    public static class UnityEventFilter
    {
        // These types are will always be prohibited even if they are derived from an allowed type. 
        private static readonly HashSet<Type> _prohibitedUIEventTargetTypes = new HashSet<Type>
        {
            #if VRC_CLIENT
            typeof(RenderHeads.Media.AVProVideo.MediaPlayer),
            #endif
            #if VRC_SDK_VRCSDK3
            typeof(VRCUrlInputField),
            #endif
            typeof(VideoPlayer)
        };

        private static readonly Lazy<Dictionary<Type, AllowedMethodFilter>> _allowedUnityEventTargetTypes =
            new Lazy<Dictionary<Type, AllowedMethodFilter>>(GetRuntimeUnityEventTargetAccessFilterDictionary);

        private static Dictionary<Type, AllowedMethodFilter> AllowedUnityEventTargetTypes => _allowedUnityEventTargetTypes.Value;

        private static readonly Lazy<int> _debugLevel = new Lazy<int>(InitializeLogging);
        private static int DebugLevel => _debugLevel.Value;

        // Builds a HashSet of allowed types, and their derived types, and removes explicitly prohibited types. 
        private static Dictionary<Type, AllowedMethodFilter> GetRuntimeUnityEventTargetAccessFilterDictionary()
        {
            Dictionary<Type, AllowedMethodFilter> accessFilterDictionary = new Dictionary<Type, AllowedMethodFilter>(_initialTargetAccessFilters);
            AddDerivedTypes(accessFilterDictionary);
            RemoveProhibitedTypes(accessFilterDictionary);

            #if VERBOSE_EVENT_SANITIZATION_LOGGING
            StringBuilder stringBuilder = new StringBuilder();
            foreach(KeyValuePair<Type, AllowedMethodFilter> entry in accessFilterDictionary)
            {
                stringBuilder.AppendLine(entry.Key.FullName);
                AllowedMethodFilter targetMethodAccessFilter = entry.Value;
                foreach(string targetMethod in targetMethodAccessFilter.GetTargetMethodNames())
                {
                    stringBuilder.AppendLine($"    {targetMethod}");
                }

                stringBuilder.AppendLine();
            }

            VerboseLog(stringBuilder.ToString());
            #endif

            return accessFilterDictionary;
        }

        #if !VRC_CLIENT && UNITY_EDITOR && VRC_SDK_VRCSDK3
        [RuntimeInitializeOnLoadMethod]
        private static void SetupPlayMode()
        {
            EditorApplication.playModeStateChanged += RunFilteringOnPlayModeEntry;
        }

        private static void RunFilteringOnPlayModeEntry(PlayModeStateChange playModeStateChange)
        {
            switch(playModeStateChange)
            {
                case PlayModeStateChange.EnteredPlayMode:
                {
                    for(int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
                    {
                        Scene currentScene = SceneManager.GetSceneAt(sceneIndex);
                        List<GameObject> rootGameObjects = new List<GameObject>();
                        currentScene.GetRootGameObjects(rootGameObjects);

                        FilterEvents(rootGameObjects);
                    }

                    break;
                }
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                {
                    return;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(playModeStateChange), playModeStateChange, null);
                }
            }
        }
        #endif

        private static int InitializeLogging()
        {
            int hashCode = typeof(UnityEventFilter).GetHashCode();
            Logger.DescribeDebugLevel(hashCode, "UnityEventFilter", Logger.Color.red);
            Logger.AddDebugLevel(hashCode);
            return hashCode;
        }

        [PublicAPI]
        public static void FilterEvents(GameObject gameObject)
        {
            FilterUIEvents(gameObject);
            FilterEventTriggerEvents(gameObject);
            FilterAnimatorEvents(gameObject);
        }

        [PublicAPI]
        public static void FilterEvents(List<GameObject> gameObjects)
        {
            FilterUIEvents(gameObjects);
            FilterEventTriggerEvents(gameObjects);
            FilterAnimatorEvents(gameObjects);
        }

        [PublicAPI]
        public static void FilterUIEvents(GameObject gameObject)
        {
            List<UIBehaviour> uiBehaviours = new List<UIBehaviour>();
            gameObject.GetComponentsInChildren(true, uiBehaviours);

            FilterUIBehaviourEvents(uiBehaviours);
        }

        [PublicAPI]
        public static void FilterUIEvents(List<GameObject> gameObjects)
        {
            HashSet<UIBehaviour> uiBehaviours = new HashSet<UIBehaviour>();
            List<UIBehaviour> uiBehavioursWorkingList = new List<UIBehaviour>();
            foreach(GameObject gameObject in gameObjects)
            {
                gameObject.GetComponentsInChildren(true, uiBehavioursWorkingList);
                uiBehaviours.UnionWith(uiBehavioursWorkingList);
            }

            FilterUIBehaviourEvents(uiBehaviours);
        }

        [PublicAPI]
        public static void FilterEventTriggerEvents(GameObject gameObject)
        {
            List<EventTrigger> eventTriggers = new List<EventTrigger>();
            gameObject.GetComponentsInChildren(true, eventTriggers);

            FilterEventTriggerEvents(eventTriggers);
        }

        [PublicAPI]
        public static void FilterEventTriggerEvents(List<GameObject> gameObjects)
        {
            HashSet<EventTrigger> eventTriggers = new HashSet<EventTrigger>();
            List<EventTrigger> eventTriggerWorkingList = new List<EventTrigger>();
            foreach(GameObject gameObject in gameObjects)
            {
                gameObject.GetComponentsInChildren(true, eventTriggerWorkingList);
                eventTriggers.UnionWith(eventTriggerWorkingList);
            }

            FilterEventTriggerEvents(eventTriggers);
        }

        [PublicAPI]
        public static void FilterAnimatorEvents(GameObject gameObject)
        {
            List<Animator> animators = new List<Animator>();
            gameObject.GetComponentsInChildren(true, animators);

            FilterAnimatorEvents(animators);
        }

        [PublicAPI]
        public static void FilterAnimatorEvents(List<GameObject> gameObjects)
        {
            HashSet<Animator> animators = new HashSet<Animator>();
            List<Animator> animatorsWorkingList = new List<Animator>();
            foreach(GameObject gameObject in gameObjects)
            {
                gameObject.GetComponentsInChildren(true, animatorsWorkingList);
                animators.UnionWith(animatorsWorkingList);
            }

            FilterAnimatorEvents(animators);
        }

        private static void FilterUIBehaviourEvents(IEnumerable<UIBehaviour> uiBehaviours)
        {
            Dictionary<Type, List<UIBehaviour>> uiBehavioursByType = new Dictionary<Type, List<UIBehaviour>>();
            foreach(UIBehaviour uiBehaviour in uiBehaviours)
            {
                if(uiBehaviour == null)
                {
                    continue;
                }

                Type uiBehaviourType = uiBehaviour.GetType();
                if(!uiBehavioursByType.TryGetValue(uiBehaviourType, out List<UIBehaviour> uiBehavioursOfType))
                {
                    uiBehavioursByType.Add(uiBehaviourType, new List<UIBehaviour> {uiBehaviour});
                    continue;
                }

                uiBehavioursOfType.Add(uiBehaviour);
            }

            foreach(KeyValuePair<Type, List<UIBehaviour>> uiBehavioursOfTypeKvp in uiBehavioursByType)
            {
                Type uiBehaviourType = uiBehavioursOfTypeKvp.Key;
                FieldInfo[] fieldInfos = uiBehaviourType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                List<FieldInfo> unityEventFieldInfos = new List<FieldInfo>();
                foreach(FieldInfo fieldInfo in fieldInfos)
                {
                    if(typeof(UnityEventBase).IsAssignableFrom(fieldInfo.FieldType))
                    {
                        unityEventFieldInfos.Add(fieldInfo);
                    }
                }

                if(unityEventFieldInfos.Count <= 0)
                {
                    continue;
                }

                FieldInfo persistentCallsGroupFieldInfo = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);
                if(persistentCallsGroupFieldInfo == null)
                {
                    VerboseLog($"Could not find 'm_PersistentCalls' on UnityEventBase.");
                    return;
                }

                foreach(UIBehaviour uiBehaviour in uiBehavioursOfTypeKvp.Value)
                {
                    VerboseLog($"Checking '{uiBehaviour.name} for UI Events.", uiBehaviour);
                    foreach(FieldInfo unityEventFieldInfo in unityEventFieldInfos)
                    {
                        VerboseLog($"Checking field '{unityEventFieldInfo.Name}' on '{uiBehaviour.name}.", uiBehaviour);
                        UnityEventBase unityEventBase = unityEventFieldInfo.GetValue(uiBehaviour) as UnityEventBase;
                        if(unityEventBase == null)
                        {
                            VerboseLog($"Null '{unityEventFieldInfo.Name}' UnityEvent on {uiBehaviour.name}.", uiBehaviour);
                            continue;
                        }

                        int numEventListeners = unityEventBase.GetPersistentEventCount();
                        VerboseLog($"There are '{numEventListeners}' on event '{unityEventFieldInfo.Name}' on '{uiBehaviour.name}.", uiBehaviour);
                        for(int index = 0; index < numEventListeners; index++)
                        {
                            string persistentMethodName = unityEventBase.GetPersistentMethodName(index);

                            UnityEngine.Object persistentTarget = unityEventBase.GetPersistentTarget(index);
                            if(persistentTarget == null)
                            {
                                VerboseLog($"The target for listener '{index}' on event '{unityEventFieldInfo.Name}' on '{uiBehaviour.name} is null.", uiBehaviour);
                                continue;
                            }

                            if(IsTargetPermitted(persistentTarget, persistentMethodName))
                            {
                                VerboseLog(
                                    $"Allowing event '{unityEventFieldInfo.Name}' on '{uiBehaviour.name}' to call '{persistentMethodName}' on target '{persistentTarget.name}'.",
                                    uiBehaviour);

                                continue;
                            }

                            LogRemoval(
                                $"Events on '{uiBehaviour.name}' were removed because one of them targeted a prohibited type '{persistentTarget.GetType().Name}', method '{persistentMethodName}' or object '{persistentTarget.name}'.",
                                uiBehaviour);

                            unityEventFieldInfo.SetValue(uiBehaviour, Activator.CreateInstance(unityEventBase.GetType()));
                            break;
                        }
                    }
                }
            }
        }

        private static void FilterEventTriggerEvents(IEnumerable<EventTrigger> eventTriggers)
        {
            FieldInfo persistentCallsGroupFieldInfo = typeof(UnityEventBase).GetField("m_PersistentCalls", BindingFlags.Instance | BindingFlags.NonPublic);
            if(persistentCallsGroupFieldInfo == null)
            {
                VerboseLog($"Could not find 'm_PersistentCalls' on UnityEventBase.");
                return;
            }

            foreach(EventTrigger eventTrigger in eventTriggers)
            {
                VerboseLog($"Checking '{eventTrigger.name} for Unity Events.", eventTrigger);

                List<EventTrigger.Entry> triggers = eventTrigger.triggers;
                if(triggers.Count <= 0)
                {
                    continue;
                }

                for(int i = triggers.Count - 1; i >= 0; i--)
                {
                    EventTrigger.Entry entry = triggers[i];
                    UnityEventBase unityEventBase = entry.callback;
                    if(unityEventBase == null)
                    {
                        VerboseLog($"Null '{entry.eventID}' UnityEvent on {eventTrigger.name}.", eventTrigger);
                        continue;
                    }

                    int numEventListeners = unityEventBase.GetPersistentEventCount();
                    VerboseLog($"There are '{numEventListeners}' on event '{entry.eventID}' on '{eventTrigger.name}.", eventTrigger);
                    for(int index = 0; index < numEventListeners; index++)
                    {
                        string persistentMethodName = unityEventBase.GetPersistentMethodName(index);

                        UnityEngine.Object persistentTarget = unityEventBase.GetPersistentTarget(index);
                        if(persistentTarget == null)
                        {
                            VerboseLog($"The target for listener '{index}' on event '{entry.eventID}' on '{eventTrigger.name} is null.", eventTrigger);
                            continue;
                        }

                        if(IsTargetPermitted(persistentTarget, persistentMethodName))
                        {
                            VerboseLog(
                                $"Allowing event '{entry.eventID}' on '{eventTrigger.name}' to call '{persistentMethodName}' on target '{persistentTarget.name}'.",
                                eventTrigger);

                            continue;
                        }

                        LogRemoval(
                            $"Events on '{eventTrigger.name}' were removed because one of them targeted a prohibited type '{persistentTarget.GetType().Name}', method '{persistentMethodName}' or object '{persistentTarget.name}'.",
                            eventTrigger);

                        triggers.RemoveAt(i);
                        break;
                    }
                }
            }
        }

        private static void FilterAnimatorEvents(IEnumerable<Animator> animators)
        {
            foreach(Animator animator in animators)
            {
                if(animator == null)
                {
                    continue;
                }

                RuntimeAnimatorController animatorController = animator.runtimeAnimatorController;
                if(animatorController == null)
                {
                    return;
                }

                foreach(AnimationClip animationClip in animatorController.animationClips)
                {
                    if(animationClip == null)
                    {
                        continue;
                    }

                    foreach(AnimationEvent animationEvent in animationClip.events)
                    {
                        if(animationEvent == null)
                        {
                            continue;
                        }

                        string animationEventFunctionName = animationEvent.functionName;
                        if(_allowedAnimationEventFunctionNames.Contains(animationEventFunctionName))
                        {
                            continue;
                        }

                        animationClip.events = null;
                        LogRemoval(
                            $"Removed AnimationEvents from AnimationClip used by the Animator on '{animator.gameObject}' because the event targets '{animationEventFunctionName}' which is not allowed.");

                        break;
                    }
                }
            }
        }

        [Conditional("VERBOSE_EVENT_SANITIZATION_LOGGING")]
        private static void VerboseLog(string message, UnityEngine.Object target = null)
        {
            Logger.LogWarning(message, DebugLevel, target);
        }

        private static void LogRemoval(string message, UnityEngine.Object target = null)
        {
            Logger.LogWarning(message, DebugLevel, target);
        }

        private static bool IsTargetPermitted(UnityEngine.Object target, string targetMethod)
        {
            // Block anything blacklisted by Udon to prevent UnityEvents from being used to bypass the blacklist.
            // NOTE: This will only block events targeting objects that are blacklisted before the UnityEventSanitizer is run.
            //       If objects are added to the blacklist after scene loading has finished it will be necessary to re-run the UnityEventSanitizer.
            #if UDON
            if(UdonManager.Instance.IsBlacklisted(target))
            {
                return false;
            }
            #endif

            Type persistentTargetType = target.GetType();
            if(!AllowedUnityEventTargetTypes.TryGetValue(persistentTargetType, out AllowedMethodFilter accessFilter))
            {
                return false;
            }

            return accessFilter.IsTargetMethodAllowed(targetMethod);
        }

        // Adds types derived from whitelisted types.
        private static void AddDerivedTypes(Dictionary<Type, AllowedMethodFilter> accessFilterDictionary)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach(Assembly assembly in assemblies)
            {
                foreach(Type type in assembly.GetTypes())
                {
                    if(accessFilterDictionary.ContainsKey(type))
                    {
                        continue;
                    }

                    if(!typeof(Component).IsAssignableFrom(type))
                    {
                        continue;
                    }

                    Type currentType = type;
                    while(currentType != typeof(object) && currentType != null)
                    {
                        if(accessFilterDictionary.TryGetValue(currentType, out AllowedMethodFilter accessFilter))
                        {
                            accessFilterDictionary.Add(type, accessFilter);
                            break;
                        }

                        currentType = currentType.BaseType;
                    }
                }
            }
        }

        // Removes prohibited types and types derived from them.
        private static void RemoveProhibitedTypes(Dictionary<Type, AllowedMethodFilter> accessFilterDictionary)
        {
            foreach(Type prohibitedType in _prohibitedUIEventTargetTypes)
            {
                foreach(Type accessFilterType in accessFilterDictionary.Keys.ToArray())
                {
                    if(prohibitedType.IsAssignableFrom(accessFilterType))
                    {
                        accessFilterDictionary.Remove(accessFilterType);
                    }
                }
            }
        }

        private static readonly Dictionary<Type, AllowedMethodFilter> _initialTargetAccessFilters = new Dictionary<Type, AllowedMethodFilter>
        {
            {
                typeof(GameObject), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(GameObject.SetActive)
                    },
                    new List<string>())
            },
            {
                typeof(AudioSource),
                new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(AudioSource.Pause),
                        nameof(AudioSource.Play),
                        nameof(AudioSource.PlayDelayed),
                        nameof(AudioSource.PlayOneShot),
                        nameof(AudioSource.Stop),
                        nameof(AudioSource.UnPause)
                    },
                    new List<string>
                    {
                        nameof(AudioSource.bypassEffects),
                        nameof(AudioSource.bypassListenerEffects),
                        nameof(AudioSource.bypassReverbZones),
                        nameof(AudioSource.dopplerLevel),
                        nameof(AudioSource.enabled),
                        nameof(AudioSource.loop),
                        nameof(AudioSource.maxDistance),
                        nameof(AudioSource.rolloffMode),
                        nameof(AudioSource.minDistance),
                        nameof(AudioSource.mute),
                        nameof(AudioSource.pitch),
                        nameof(AudioSource.playOnAwake),
                        nameof(AudioSource.priority),
                        nameof(AudioSource.spatialize),
                        nameof(AudioSource.spread),
                        nameof(AudioSource.time),
                        nameof(AudioSource.volume)
                    }
                )
            },
            {
                typeof(AudioDistortionFilter), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioDistortionFilter.distortionLevel),
                        nameof(AudioDistortionFilter.enabled)
                    })
            },
            {
                typeof(AudioEchoFilter), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioEchoFilter.decayRatio),
                        nameof(AudioEchoFilter.delay),
                        nameof(AudioEchoFilter.dryMix),
                        nameof(AudioEchoFilter.enabled),
                        nameof(AudioEchoFilter.wetMix)
                    })
            },
            {
                typeof(AudioHighPassFilter), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioHighPassFilter.cutoffFrequency),
                        nameof(AudioHighPassFilter.enabled),
                        nameof(AudioHighPassFilter.highpassResonanceQ)
                    })
            },
            {
                typeof(AudioLowPassFilter), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioLowPassFilter.cutoffFrequency),
                        nameof(AudioLowPassFilter.enabled),
                        nameof(AudioLowPassFilter.lowpassResonanceQ)
                    })
            },
            {
                typeof(AudioReverbFilter), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioReverbFilter.decayHFRatio),
                        nameof(AudioReverbFilter.decayTime),
                        nameof(AudioReverbFilter.density),
                        nameof(AudioReverbFilter.diffusion),
                        nameof(AudioReverbFilter.dryLevel),
                        nameof(AudioReverbFilter.enabled),
                        nameof(AudioReverbFilter.hfReference),
                        nameof(AudioReverbFilter.reflectionsDelay),
                        nameof(AudioReverbFilter.reflectionsLevel),
                        nameof(AudioReverbFilter.reverbDelay),
                        nameof(AudioReverbFilter.reverbLevel),
                        nameof(AudioReverbFilter.room),
                        nameof(AudioReverbFilter.roomHF),
                        nameof(AudioReverbFilter.roomLF)
                    })
            },
            {
                typeof(AudioReverbZone), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(AudioReverbZone.decayHFRatio),
                        nameof(AudioReverbZone.decayTime),
                        nameof(AudioReverbZone.density),
                        nameof(AudioReverbZone.diffusion),
                        nameof(AudioReverbZone.enabled),
                        nameof(AudioReverbZone.HFReference),
                        nameof(AudioReverbZone.LFReference),
                        nameof(AudioReverbZone.maxDistance),
                        nameof(AudioReverbZone.minDistance),
                        nameof(AudioReverbZone.reflections),
                        nameof(AudioReverbZone.reflectionsDelay),
                        nameof(AudioReverbZone.room),
                        nameof(AudioReverbZone.roomHF),
                        nameof(AudioReverbZone.roomLF)
                    })
            },
            #if UDON
            {
                typeof(UdonBehaviour), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(UdonBehaviour.RunProgram),
                        nameof(UdonBehaviour.SendCustomEvent),
                        nameof(UdonBehaviour.Interact),
                    },
                    new List<string>()
                    {
                        nameof(UdonBehaviour.enabled)
                    })
            },
            #endif
            {
                typeof(MeshRenderer), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(MeshRenderer.shadowCastingMode),
                        nameof(MeshRenderer.enabled),
                        nameof(MeshRenderer.probeAnchor),
                        nameof(MeshRenderer.probeAnchor),
                        nameof(MeshRenderer.receiveShadows),
                        nameof(MeshRenderer.lightProbeUsage)
                    })
            },
            {
                typeof(Collider), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Collider.enabled),
                        nameof(Collider.isTrigger)
                    })
            },
            {
                typeof(SkinnedMeshRenderer), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(SkinnedMeshRenderer.allowOcclusionWhenDynamic),
                        nameof(SkinnedMeshRenderer.shadowCastingMode),
                        nameof(SkinnedMeshRenderer.enabled),
                        nameof(SkinnedMeshRenderer.lightProbeProxyVolumeOverride),
                        nameof(SkinnedMeshRenderer.motionVectorGenerationMode),
                        nameof(SkinnedMeshRenderer.probeAnchor),
                        nameof(SkinnedMeshRenderer.receiveShadows),
                        nameof(SkinnedMeshRenderer.rootBone),
                        nameof(SkinnedMeshRenderer.skinnedMotionVectors),
                        nameof(SkinnedMeshRenderer.updateWhenOffscreen),
                        nameof(SkinnedMeshRenderer.lightProbeUsage)
                    })
            },
            {
                typeof(Light), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(Light.Reset)
                    },
                    new List<string>
                    {
                        nameof(Light.bounceIntensity),
                        nameof(Light.colorTemperature),
                        nameof(Light.cookie),
                        nameof(Light.enabled),
                        nameof(Light.intensity),
                        nameof(Light.range),
                        nameof(Light.shadowBias),
                        nameof(Light.shadowNearPlane),
                        nameof(Light.shadowNormalBias),
                        nameof(Light.shadowStrength),
                        nameof(Light.spotAngle)
                    })
            },
            {
                typeof(ParticleSystem), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(ParticleSystem.Clear),
                        nameof(ParticleSystem.Emit),
                        nameof(ParticleSystem.Pause),
                        nameof(ParticleSystem.Pause),
                        nameof(ParticleSystem.Play),
                        nameof(ParticleSystem.Simulate),
                        nameof(ParticleSystem.Stop),
                        nameof(ParticleSystem.Stop),
                        nameof(ParticleSystem.TriggerSubEmitter)
                    },
                    new List<string>
                    {
                        nameof(ParticleSystem.time),
                        nameof(ParticleSystem.useAutoRandomSeed)
                    })
            },
            {
                typeof(ParticleSystemForceField), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(ParticleSystemForceField.endRange),
                        nameof(ParticleSystemForceField.gravityFocus),
                        nameof(ParticleSystemForceField.length),
                        nameof(ParticleSystemForceField.multiplyDragByParticleSize),
                        nameof(ParticleSystemForceField.multiplyDragByParticleVelocity),
                        nameof(ParticleSystemForceField.startRange)
                    })
            },
            {
                typeof(Projector), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Projector.aspectRatio),
                        nameof(Projector.enabled),
                        nameof(Projector.nearClipPlane),
                        nameof(Projector.farClipPlane),
                        nameof(Projector.fieldOfView),
                        nameof(Projector.orthographic),
                        nameof(Projector.orthographicSize)
                    })
            },
            {
                typeof(LineRenderer), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(LineRenderer.allowOcclusionWhenDynamic),
                        nameof(LineRenderer.shadowCastingMode),
                        nameof(LineRenderer.enabled),
                        nameof(LineRenderer.endWidth),
                        nameof(LineRenderer.loop),
                        nameof(LineRenderer.motionVectorGenerationMode),
                        nameof(LineRenderer.numCapVertices),
                        nameof(LineRenderer.numCornerVertices),
                        nameof(LineRenderer.probeAnchor),
                        nameof(LineRenderer.receiveShadows),
                        nameof(LineRenderer.shadowBias),
                        nameof(LineRenderer.startWidth),
                        nameof(LineRenderer.lightProbeUsage),
                        nameof(LineRenderer.useWorldSpace),
                        nameof(LineRenderer.widthMultiplier)
                    })
            },
            {
                typeof(TrailRenderer), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(TrailRenderer.Clear)
                    },
                    new List<string>
                    {
                        nameof(TrailRenderer.allowOcclusionWhenDynamic),
                        nameof(TrailRenderer.autodestruct),
                        nameof(TrailRenderer.shadowCastingMode),
                        nameof(TrailRenderer.enabled),
                        nameof(TrailRenderer.emitting),
                        nameof(TrailRenderer.endWidth),
                        nameof(TrailRenderer.motionVectorGenerationMode),
                        nameof(TrailRenderer.numCapVertices),
                        nameof(TrailRenderer.numCornerVertices),
                        nameof(TrailRenderer.probeAnchor),
                        nameof(TrailRenderer.receiveShadows),
                        nameof(TrailRenderer.shadowBias),
                        nameof(TrailRenderer.startWidth),
                        nameof(TrailRenderer.lightProbeUsage),
                        nameof(TrailRenderer.widthMultiplier)
                    })
            },
            {
                typeof(Animator), new AllowedMethodFilter(
                    new List<string>
                    {
                        nameof(Animator.Play),
                        nameof(Animator.PlayInFixedTime),
                        nameof(Animator.Rebind),
                        nameof(Animator.SetBool),
                        nameof(Animator.SetFloat),
                        nameof(Animator.SetInteger),
                        nameof(Animator.SetTrigger),
                        nameof(Animator.ResetTrigger)
                    },
                    new List<string>
                    {
                        nameof(Animator.speed),
                        nameof(Animator.enabled)
                    })
            },
            {
                typeof(Text), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Text.alignByGeometry),
                        nameof(Text.enabled),
                        nameof(Text.fontSize),
                        nameof(Text.lineSpacing),
                        nameof(Text.maskable),
                        nameof(Text.raycastTarget),
                        nameof(Text.resizeTextForBestFit),
                        nameof(Text.resizeTextMaxSize),
                        nameof(Text.resizeTextMinSize),
                        nameof(Text.supportRichText),
                        nameof(Text.text)
                    })
            },
            {
                typeof(Image), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Image.alphaHitTestMinimumThreshold),
                        nameof(Image.enabled),
                        nameof(Image.fillAmount),
                        nameof(Image.fillCenter),
                        nameof(Image.fillClockwise),
                        nameof(Image.fillOrigin),
                        nameof(Image.maskable),
                        nameof(Image.preserveAspect),
                        nameof(Image.raycastTarget),
                        nameof(Image.useSpriteMesh)
                    })
            },
            {
                typeof(RawImage), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(RawImage.enabled),
                        nameof(RawImage.maskable),
                        nameof(RawImage.raycastTarget)
                    })
            },
            {
                typeof(InputField), new AllowedMethodFilter(
                    new List<string>
                    {
                        "Append",
                        nameof(InputField.ForceLabelUpdate)
                    },
                    new List<string>
                    {
                        nameof(InputField.caretBlinkRate),
                        nameof(InputField.caretPosition),
                        nameof(InputField.caretWidth),
                        nameof(InputField.characterLimit),
                        nameof(InputField.customCaretColor),
                        nameof(InputField.enabled),
                        nameof(InputField.interactable),
                        nameof(InputField.readOnly),
                        nameof(InputField.selectionAnchorPosition),
                        nameof(InputField.text),
                        nameof(InputField.textComponent),
                        nameof(InputField.selectionFocusPosition)
                    })
            },
            {
                typeof(Dropdown), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Dropdown.captionText),
                        nameof(Dropdown.enabled),
                        nameof(Dropdown.interactable),
                        nameof(Dropdown.itemText),
                        nameof(Dropdown.targetGraphic),
                        nameof(Dropdown.template),
                        nameof(Dropdown.value)
                    })
            },
            {
                typeof(Slider), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Slider.enabled),
                        nameof(Slider.fillRect),
                        nameof(Slider.handleRect),
                        nameof(Slider.interactable),
                        nameof(Slider.maxValue),
                        nameof(Slider.minValue),
                        nameof(Slider.normalizedValue),
                        nameof(Slider.targetGraphic),
                        nameof(Slider.value),
                        nameof(Slider.wholeNumbers)
                    })
            },
            {
                typeof(Toggle), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Toggle.enabled),
                        nameof(Toggle.group),
                        nameof(Toggle.interactable),
                        nameof(Toggle.isOn),
                        nameof(Toggle.targetGraphic)
                    })
            },
            {
                typeof(Scrollbar), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Scrollbar.enabled),
                        nameof(Scrollbar.handleRect),
                        nameof(Scrollbar.interactable),
                        nameof(Scrollbar.numberOfSteps),
                        nameof(Scrollbar.size),
                        nameof(Scrollbar.targetGraphic),
                        nameof(Scrollbar.value)
                    })
            },
            {
                typeof(ScrollRect), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(ScrollRect.content),
                        nameof(ScrollRect.decelerationRate),
                        nameof(ScrollRect.elasticity),
                        nameof(ScrollRect.enabled),
                        nameof(ScrollRect.horizontal),
                        nameof(ScrollRect.horizontalNormalizedPosition),
                        nameof(ScrollRect.horizontalScrollbar),
                        nameof(ScrollRect.horizontalScrollbarSpacing),
                        nameof(ScrollRect.inertia),
                        nameof(ScrollRect.scrollSensitivity),
                        nameof(ScrollRect.vertical),
                        nameof(ScrollRect.verticalNormalizedPosition),
                        nameof(ScrollRect.verticalScrollbar),
                        nameof(ScrollRect.verticalScrollbarSpacing),
                        nameof(ScrollRect.viewport)
                    })
            },
            {
                typeof(Button), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Button.enabled),
                        nameof(Button.interactable),
                        nameof(Button.targetGraphic)
                    })
            },
            {
                typeof(Mask), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Mask.enabled),
                        nameof(Mask.showMaskGraphic)
                    })
            },
            {
                typeof(RectMask2D), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(RectMask2D.enabled)
                    })
            },
            {
                typeof(Selectable), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(Selectable.enabled),
                        nameof(Selectable.interactable),
                        nameof(Selectable.targetGraphic)
                    })
            },
            {
                typeof(ToggleGroup), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(ToggleGroup.allowSwitchOff),
                        nameof(ToggleGroup.enabled)
                    })
            },
            #if VRC_SDK_VRCSDK3 // only access Cinemachine and TMPro after install
            {
                typeof(TextMeshPro), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(TextMeshPro.text),
                    })
            },
            {
                typeof(TextMeshProUGUI), new AllowedMethodFilter(
                    new List<string>(),
                    new List<string>
                    {
                        nameof(TextMeshProUGUI.text),
                    })
            },
            {
                typeof(Cinemachine.CinemachineVirtualCamera), new AllowedMethodFilter(
                    new List<string>()
                    {
                        nameof(Cinemachine.CinemachineVirtualCamera.Priority),
                    },
                    new List<string>())
            },
            #endif
        };

        private static readonly HashSet<string> _allowedAnimationEventFunctionNames = new HashSet<string>
        {
            "RunProgram",
            "SendCustomEvent",
            "Play",
            "Pause",
            "Stop",
            "PlayInFixedTime",
            "Rebind",
            "SetBool",
            "SetFloat",
            "SetInteger",
            "SetTrigger",
            "ResetTrigger",
            "SetActive"
        };

        private class AllowedMethodFilter
        {
            private readonly HashSet<string> _allowedTargets;

            [PublicAPI]
            public AllowedMethodFilter(List<string> allowedTargetMethodNames, List<string> allowedTargetPropertyNames)
            {
                _allowedTargets = new HashSet<string>();
                _allowedTargets.UnionWith(allowedTargetMethodNames);
                foreach(string allowedTargetProperty in allowedTargetPropertyNames)
                {
                    _allowedTargets.Add($"get_{allowedTargetProperty}");
                    _allowedTargets.Add($"set_{allowedTargetProperty}");
                }
            }

            public bool IsTargetMethodAllowed(string targetMethodName)
            {
                return _allowedTargets.Contains(targetMethodName);
            }

            #if VERBOSE_EVENT_SANITIZATION_LOGGING
            public List<string> GetTargetMethodNames()
            {
                return _allowedTargets.ToList();
            }
            #endif
        }
    }
}
