using System.Collections.Generic;
using System.Collections;
using Game.Interaction;
using UnityEngine;
using UnityEngine.AI;

namespace OutOfSight.Environment
{
    [DisallowMultipleComponent]
    public sealed class IntroductionSequenceController : MonoBehaviour
    {
        [Header("Doors")]
        [SerializeField] private string introDoorNamePrefix = "door1";
        [SerializeField] private List<string> apartmentEntryDoorNames = new List<string> { "door1 (1)" };
        [SerializeField] private List<string> allowedDoorNames = new List<string> { "door1 (6)", "door1 (2)" };
        [SerializeField] private string blockedDoorPrompt = "Open";
        [SerializeField] private string blockedDoorSubtitleText = "Сначала надо взять ключи из гостиной.";
        [SerializeField, Min(0.1f)] private float blockedDoorSubtitleDuration = 2.5f;
        [SerializeField] private Transform blockedDoorPlaybackSource;
        [SerializeField] private GameObject unlockProgressObject;
        [SerializeField] private bool releaseDoorRestrictionsAfterMonsterLook;

        [Header("Look Subtitle")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private Transform lookTarget;
        [SerializeField] private LayerMask lineOfSightMask = ~0;
        [SerializeField, Min(0.1f)] private float lookDistance = 20f;
        [SerializeField, Min(0f)] private float requiredLookTime = 0.1f;
        [SerializeField] private string subtitleText = "Что это такое?..";
        [SerializeField, Min(0.1f)] private float subtitleDuration = 2.5f;
        [SerializeField] private bool triggerSubtitleOnce = true;

        [Header("Monster Event")]
        [SerializeField] private DialogueLine[] monsterSightDialogueLines =
        {
            new DialogueLine("Что за чертовщина? Надо отвлечь эту тварь.", null, 1f, 3f, 0f)
        };
        [SerializeField] private GameObject monsterProxyObject;
        [SerializeField] private GameObject gameplayMonsterObject;
        [SerializeField] private Transform gameplayMonsterSpawnPoint;
        [SerializeField] private Transform televisionTarget;
        [SerializeField] private Transform monsterSpawnTarget;
        [SerializeField] private AudioSource televisionAudioSource;
        [SerializeField] private AudioClip televisionActivateClip;
        [SerializeField] private AudioSource monsterReactionAudioSource;
        [SerializeField] private AudioClip monsterReactionClip;
        [SerializeField, Min(0.1f)] private float monsterMoveDuration = 1.75f;
        [SerializeField, Min(0f)] private float monsterActivationDelay = 0.15f;

        private readonly List<IntroductionDoorRestriction> restrictedDoorOverrides = new List<IntroductionDoorRestriction>();
        private float currentLookTime;
        private bool subtitleTriggered;
        private bool doorRestrictionsReleased;
        private bool trackUnlockProgressObject;
        private bool hasEnteredApartment;
        private bool hasSeenMonster;
        private bool hasTelevisionRemote;
        private bool televisionActivated;
        private Coroutine televisionActivationRoutine;

        public bool HasSeenMonster => hasSeenMonster;
        public bool HasTelevisionRemote => hasTelevisionRemote;
        public bool HasEnteredApartment => hasEnteredApartment;
        public bool TelevisionActivated => televisionActivated;

        private void Awake()
        {
            ResolveReferences();
            trackUnlockProgressObject = unlockProgressObject != null;
        }

        private void OnEnable()
        {
            doorRestrictionsReleased = false;
            hasEnteredApartment = false;
            hasSeenMonster = false;
            hasTelevisionRemote = false;
            televisionActivated = false;
            subtitleTriggered = false;
            currentLookTime = 0f;
            ApplyDoorRestrictions();
            SetGameplayMonsterActive(false);
            SetMonsterProxyActive(true);
        }

        private void OnDisable()
        {
            DisableDoorRestrictions();
        }

        private void OnValidate()
        {
            if (lookDistance < 0.1f)
                lookDistance = 0.1f;

            if (subtitleDuration < 0.1f)
                subtitleDuration = 0.1f;

            if (requiredLookTime < 0f)
                requiredLookTime = 0f;
        }

        private void Update()
        {
            if (!doorRestrictionsReleased && trackUnlockProgressObject && (unlockProgressObject == null || !unlockProgressObject.activeInHierarchy))
            {
                ReleaseDoorRestrictions();
                return;
            }

            if (triggerSubtitleOnce && subtitleTriggered)
                return;

            if (playerCamera == null || lookTarget == null)
                return;

            if (!IsLookingDirectlyAtTarget())
            {
                currentLookTime = 0f;
                return;
            }

            currentLookTime += Time.deltaTime;
            if (currentLookTime < requiredLookTime)
                return;

            currentLookTime = 0f;
            subtitleTriggered = true;
            hasSeenMonster = true;
            ShowMonsterSightDialogue();
            ReleaseDoorRestrictions();
        }

        [ContextMenu("Reapply Intro Door Restrictions")]
        public void ApplyDoorRestrictions()
        {
            restrictedDoorOverrides.Clear();

            var doors = FindObjectsByType<DoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var door in doors)
                ApplyDoorRestriction(door != null ? door.gameObject : null);

            var lockedDoors = FindObjectsByType<LockedDoorInteractable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var door in lockedDoors)
                ApplyDoorRestriction(door != null ? door.gameObject : null);
        }

        private void ApplyDoorRestriction(GameObject doorObject)
        {
            if (doorObject == null)
                return;

            GameObject restrictionHost = ResolveRestrictionHost(doorObject);
            if (restrictionHost == null)
                return;

            bool shouldStayOpenable = GetCurrentAllowedDoorNames().Contains(restrictionHost.name);
            var doorRestriction = restrictionHost.GetComponent<IntroductionDoorRestriction>();
            if (shouldStayOpenable)
            {
                if (doorRestriction != null)
                    doorRestriction.SetBlocking(false);

                return;
            }

            if (doorRestriction == null)
                doorRestriction = restrictionHost.AddComponent<IntroductionDoorRestriction>();

            doorRestriction.Configure(blockedDoorPrompt, blockedDoorSubtitleText, blockedDoorSubtitleDuration, blockedDoorPlaybackSource);
            doorRestriction.SetBlocking(!doorRestrictionsReleased);
            restrictedDoorOverrides.Add(doorRestriction);
        }

        private GameObject ResolveRestrictionHost(GameObject doorObject)
        {
            if (doorObject == null || string.IsNullOrWhiteSpace(introDoorNamePrefix))
                return null;

            Transform current = doorObject.transform;
            while (current != null)
            {
                if (current.name.StartsWith(introDoorNamePrefix))
                    return current.gameObject;

                current = current.parent;
            }

            return null;
        }

        private void ResolveReferences()
        {
            if (lookTarget == null)
            {
                var targetTransform = transform.Find("Capsule");
                if (targetTransform != null)
                    lookTarget = targetTransform;
            }

            if (monsterProxyObject == null && lookTarget != null)
                monsterProxyObject = lookTarget.gameObject;

            if (playerCamera == null)
                playerCamera = Camera.main;
        }

        private HashSet<string> GetCurrentAllowedDoorNames()
        {
            var result = new HashSet<string>(apartmentEntryDoorNames);
            if (hasEnteredApartment)
            {
                foreach (string doorName in allowedDoorNames)
                    result.Add(doorName);
            }

            return result;
        }

        public void MarkApartmentEntered(GameObject interactor, DialogueLine[] dialogueLines = null, Transform dialoguePlaybackSource = null)
        {
            if (hasEnteredApartment)
                return;

            hasEnteredApartment = true;
            ApplyDoorRestrictions();

            if (dialogueLines == null || dialogueLines.Length == 0)
                return;

            GameObject playbackHost = interactor != null
                ? interactor
                : playerCamera != null
                    ? playerCamera.gameObject
                    : gameObject;

            Transform playbackSource = dialoguePlaybackSource != null
                ? dialoguePlaybackSource
                : interactor != null
                    ? interactor.transform
                    : null;

            DialogueSequencePlayer.GetOrCreate(playbackHost).Play(
                dialogueLines,
                playbackSource,
                false,
                true,
                0f,
                null);
        }

        public void MarkRemoteCollected(GameObject interactor, DialogueLine[] dialogueLines = null, Transform dialoguePlaybackSource = null)
        {
            if (hasTelevisionRemote)
                return;

            hasTelevisionRemote = true;

            if (dialogueLines == null || dialogueLines.Length == 0)
                return;

            GameObject playbackHost = interactor != null
                ? interactor
                : playerCamera != null
                    ? playerCamera.gameObject
                    : gameObject;

            Transform playbackSource = dialoguePlaybackSource != null
                ? dialoguePlaybackSource
                : interactor != null
                    ? interactor.transform
                    : null;

            DialogueSequencePlayer.GetOrCreate(playbackHost).Play(
                dialogueLines,
                playbackSource,
                false,
                true,
                0f,
                null);
        }

        public bool CanActivateTelevision(GameObject interactor)
        {
            return hasEnteredApartment &&
                   hasSeenMonster &&
                   hasTelevisionRemote &&
                   !televisionActivated &&
                   televisionActivationRoutine == null &&
                   interactor != null;
        }

        public void ActivateTelevision(GameObject interactor)
        {
            if (!CanActivateTelevision(interactor))
                return;

            televisionActivated = true;
            televisionActivationRoutine = StartCoroutine(ActivateTelevisionRoutine());
        }

        private bool IsLookingDirectlyAtTarget()
        {
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, lookDistance, lineOfSightMask, QueryTriggerInteraction.Collide))
                return false;

            var targetCollider = lookTarget.GetComponent<Collider>();
            if (targetCollider != null)
                return hit.collider == targetCollider || hit.collider.transform.IsChildOf(lookTarget);

            return hit.transform == lookTarget || hit.transform.IsChildOf(lookTarget);
        }

        private void ShowMonsterSightDialogue()
        {
            var player = DialogueSequencePlayer.GetOrCreate(playerCamera != null ? playerCamera.gameObject : gameObject);
            if (monsterSightDialogueLines != null && monsterSightDialogueLines.Length > 0)
            {
                player.Play(
                    monsterSightDialogueLines,
                    playerCamera != null ? playerCamera.transform : null,
                    false,
                    true,
                    0f,
                    null);
                return;
            }

            if (string.IsNullOrWhiteSpace(subtitleText))
                return;

            player.Play(
                new[] { new DialogueLine(subtitleText, null, 1f, subtitleDuration, 0f) },
                null,
                false,
                true,
                0f,
                null);
        }

        [ContextMenu("Release Intro Door Restrictions")]
        public void ReleaseDoorRestrictions()
        {
            doorRestrictionsReleased = true;
            DisableDoorRestrictions();
        }

        private void DisableDoorRestrictions()
        {
            foreach (var doorRestriction in restrictedDoorOverrides)
            {
                if (doorRestriction != null)
                    doorRestriction.SetBlocking(false);
            }
        }

        private IEnumerator ActivateTelevisionRoutine()
        {
            PlayClip(televisionAudioSource, televisionActivateClip);
            PlayClip(monsterReactionAudioSource, monsterReactionClip);

            Transform proxyTransform = monsterProxyObject != null ? monsterProxyObject.transform : null;
            Transform proxyTargetTransform = televisionTarget != null ? televisionTarget : proxyTransform;
            Transform spawnTargetTransform = gameplayMonsterSpawnPoint != null
                ? gameplayMonsterSpawnPoint
                : monsterSpawnTarget != null
                    ? monsterSpawnTarget
                    : proxyTargetTransform;

            if (proxyTransform != null && proxyTargetTransform != null)
            {
                Vector3 startPosition = proxyTransform.position;
                Quaternion startRotation = proxyTransform.rotation;
                float duration = Mathf.Max(0.1f, monsterMoveDuration);
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    proxyTransform.position = Vector3.Lerp(startPosition, proxyTargetTransform.position, t);
                    proxyTransform.rotation = Quaternion.Slerp(startRotation, proxyTargetTransform.rotation, t);
                    yield return null;
                }

                proxyTransform.position = proxyTargetTransform.position;
                proxyTransform.rotation = proxyTargetTransform.rotation;
            }

            if (monsterActivationDelay > 0f)
                yield return new WaitForSeconds(monsterActivationDelay);

            Vector3 spawnPosition = spawnTargetTransform != null
                ? spawnTargetTransform.position
                : proxyTargetTransform != null
                    ? proxyTargetTransform.position
                    : gameplayMonsterObject != null
                        ? gameplayMonsterObject.transform.position
                        : Vector3.zero;

            Quaternion spawnRotation = spawnTargetTransform != null
                ? spawnTargetTransform.rotation
                : proxyTargetTransform != null
                    ? proxyTargetTransform.rotation
                    : gameplayMonsterObject != null
                        ? gameplayMonsterObject.transform.rotation
                        : Quaternion.identity;

            SetMonsterProxyActive(false);
            ActivateGameplayMonster(spawnPosition, spawnRotation);
            televisionActivationRoutine = null;
        }

        private void ActivateGameplayMonster(Vector3 spawnPosition, Quaternion spawnRotation)
        {
            if (gameplayMonsterObject == null)
                return;

            gameplayMonsterObject.transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            gameplayMonsterObject.SetActive(true);

            NavMeshAgent agent = gameplayMonsterObject.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                if (NavMesh.SamplePosition(spawnPosition, out NavMeshHit navHit, 2f, NavMesh.AllAreas))
                    agent.Warp(navHit.position);
                else if (agent.isOnNavMesh)
                    agent.Warp(spawnPosition);
            }
        }

        private void SetGameplayMonsterActive(bool value)
        {
            if (gameplayMonsterObject == null)
                return;

            gameplayMonsterObject.SetActive(value);
        }

        private void SetMonsterProxyActive(bool value)
        {
            if (monsterProxyObject == null)
                return;

            monsterProxyObject.SetActive(value);
        }

        private static void PlayClip(AudioSource source, AudioClip clip)
        {
            if (source == null || clip == null)
                return;

            source.PlayOneShot(clip);
        }
    }
}
