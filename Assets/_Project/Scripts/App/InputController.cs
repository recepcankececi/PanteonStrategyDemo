using System;
using StrategyDemo.Gameplay;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StrategyDemo.App
{
    /// <summary>
    /// Reads the Gameplay action map once per frame and hands gameplay a <see cref="PointerInput"/> snapshot
    /// (ADR-001 Karar 6). Knows devices and the screen; knows nothing about game rules.
    /// </summary>
    /// <remarks>
    /// Actions are polled in <c>Update</c> rather than handled in callbacks: the "is the pointer over the UI?"
    /// check is only reliable outside Input System callbacks, and polling lets it be computed once at the top
    /// of the frame and used for every click in that frame. The component runs after the EventSystem
    /// (see <see cref="ExecutionOrder"/>), so that answer comes from this frame's UI raycast, not the last one.
    /// </remarks>
    [DefaultExecutionOrder(ExecutionOrder)]
    public sealed class InputController : MonoBehaviour
    {
        /// <summary>
        /// Script execution order: after every default-order (0) script, the EventSystem included, which
        /// updates the pointer's UI raycast in its own Update. Otherwise the order between them is undefined.
        /// </summary>
        public const int ExecutionOrder = 100;

        [SerializeField, Tooltip("Scene EventSystem; asked whether the pointer is over the UI.")]
        private EventSystem _eventSystem;

        private GameInputActions _actions;
        private Camera _camera;
        private PlacementInputHandler _placementInput;
        private SelectionInputHandler _selectionInput;
        private CommandInputHandler _commandInput;

        /// <summary>
        /// Starts routing input, reading the pointer through <paramref name="boardCamera"/>. Placement sees each
        /// frame first; selection and commands get it only when placement did not consume it.
        /// </summary>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="InvalidOperationException">The EventSystem is not assigned.</exception>
        public void Init(
            Camera boardCamera, PlacementInputHandler placementInput, SelectionInputHandler selectionInput, CommandInputHandler commandInput)
        {
            if (_eventSystem == null)
            {
                throw new InvalidOperationException($"{nameof(InputController)} on '{name}' has no EventSystem assigned.");
            }

            _camera = boardCamera != null ? boardCamera : throw new ArgumentNullException(nameof(boardCamera));
            _placementInput = placementInput ?? throw new ArgumentNullException(nameof(placementInput));
            _selectionInput = selectionInput ?? throw new ArgumentNullException(nameof(selectionInput));
            _commandInput = commandInput ?? throw new ArgumentNullException(nameof(commandInput));
        }

        private void Awake()
        {
            // The generated class builds its own copy of the asset; this component owns and disposes it.
            _actions = new GameInputActions();
        }

        private void OnEnable()
        {
            _actions.Gameplay.Enable();
        }

        private void OnDisable()
        {
            _actions.Gameplay.Disable();
        }

        private void OnDestroy()
        {
            _actions.Dispose();
        }

        private void Update()
        {
            if (_placementInput == null)
            {
                return;
            }

            GameInputActions.GameplayActions gameplay = _actions.Gameplay;
            Vector2 screen = gameplay.Point.ReadValue<Vector2>();
            Vector3 world = _camera.ScreenToWorldPoint(screen);
            world.z = 0f;

            // A struct built on the stack: nothing is allocated per frame.
            var input = new PointerInput(
                world,
                _eventSystem.IsPointerOverGameObject(),
                gameplay.Select.WasPressedThisFrame(),
                gameplay.Command.WasPressedThisFrame(),
                gameplay.Cancel.WasPressedThisFrame(),
                gameplay.Modifier.IsPressed(),
                gameplay.Select.IsPressed(),
                gameplay.Select.WasReleasedThisFrame());
            // While placing, a click places (or cancels) and must not also select or order what is under it.
            if (!_placementInput.Handle(input))
            {
                _selectionInput.Handle(input);
                // A right click while a box is being dragged would order the provisional selection, not the box.
                if (!_selectionInput.IsDragging)
                {
                    _commandInput.Handle(input);
                }
            }
        }
    }
}
