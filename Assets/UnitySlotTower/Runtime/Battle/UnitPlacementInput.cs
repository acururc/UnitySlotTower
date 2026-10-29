using UnityEngine;
using UnityEngine.InputSystem;
using UnitySlotTower.Core.Battle;

namespace UnitySlotTower.Battle
{
    /// <summary>
    /// 召喚・配置・向き選びの入力（FR-002、plan 3章）。マウスとキーボードの操作を <see cref="UnitDeployment"/> の操作に変える。
    /// マウス: 操作バーのアイコンを押してフィールドのマスで離す（クリックで選んでからマスをクリックしてもよい）。
    /// 向きは、マスを決めたあと押したまま上下左右へドラッグして離すと決まり、中心付近で離すとマス選びに戻る。
    /// キーボード: 1〜5 で選び、矢印でカーソル移動と向き、Enter で決定、Esc で1段階戻る。
    /// </summary>
    public sealed class UnitPlacementInput : MonoBehaviour
    {
        private const string ActionMap = "SlotTower";
        private const int SlotCount = 5;

        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private BattleController battle;
        [SerializeField] private BattleFieldView fieldView;
        [SerializeField] private Camera battleCamera;
        [Tooltip("向きを決めるのに必要なドラッグの長さ（マス）。")]
        [SerializeField] private float facingDragCells = 0.5f;

        private readonly InputAction[] _selects = new InputAction[SlotCount];
        private InputAction _cursor;
        private InputAction _confirm;
        private InputAction _cancel;
        private InputAction _point;
        private InputAction _press;
        private Vector2 _lastCursorValue;
        private Vector2 _lastPointer;
        private bool _facingDrag;

        private void Awake()
        {
            var map = inputActions.FindActionMap(ActionMap, true);
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i] = map.FindAction($"SelectUnit{i + 1}", true);
            }

            _cursor = map.FindAction("Cursor", true);
            _confirm = map.FindAction("Confirm", true);
            _cancel = map.FindAction("Cancel", true);
            _point = map.FindAction("Point", true);
            _press = map.FindAction("Press", true);
        }

        private void OnEnable()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i].performed += OnSelectAction;
            }

            _cursor.performed += OnCursorAction;
            _cursor.canceled += OnCursorAction;
            _confirm.performed += OnConfirmAction;
            _cancel.performed += OnCancelAction;
            _cursor.actionMap.Enable();
        }

        private void OnDisable()
        {
            for (var i = 0; i < SlotCount; i++)
            {
                _selects[i].performed -= OnSelectAction;
            }

            _cursor.performed -= OnCursorAction;
            _cursor.canceled -= OnCursorAction;
            _confirm.performed -= OnConfirmAction;
            _cancel.performed -= OnCancelAction;
        }

        /// <summary>
        /// 操作バーの slot 番目（0 始まり、units.csv の並び）の味方を選ぶ。選択中の味方をもう一度選ぶと選択をやめる。
        /// 召喚できない（コイン不足・配置上限・待ち時間）ときは何も選ばない。
        /// </summary>
        public void SelectSlot(int slot)
        {
            var deployment = battle.Deployment;
            if (deployment == null || slot < 0 || slot >= battle.Units.All.Count)
            {
                return;
            }

            var unitId = battle.Units.All[slot].Id;
            if (deployment.Selected != null && deployment.Selected.Id == unitId)
            {
                deployment.Cancel();
                if (deployment.Phase != DeployPhase.Idle)
                {
                    deployment.Cancel();
                }

                return;
            }

            deployment.Select(unitId);
        }

        private void OnSelectAction(InputAction.CallbackContext context)
        {
            // Shift + 数字は強化（plan 3章）なので選択には使わない。
            if (context.control.device is Keyboard keyboard && keyboard.shiftKey.isPressed)
            {
                return;
            }

            SelectSlot(System.Array.IndexOf(_selects, context.action));
        }

        private void OnCursorAction(InputAction.CallbackContext context)
        {
            // 押し続けで繰り返さず、新しく押された方向だけ1マス（1回）動かす。画面の上は y が小さい方。
            var value = context.ReadValue<Vector2>();
            var dx = value.x != 0f && _lastCursorValue.x == 0f ? (int)Mathf.Sign(value.x) : 0;
            var dy = dx == 0 && value.y != 0f && _lastCursorValue.y == 0f ? -(int)Mathf.Sign(value.y) : 0;
            _lastCursorValue = value;
            if (dx != 0 || dy != 0)
            {
                battle.Deployment?.MoveCursor(dx, dy);
            }
        }

        private void OnConfirmAction(InputAction.CallbackContext context)
        {
            var deployment = battle.Deployment;
            if (deployment == null)
            {
                return;
            }

            if (deployment.Phase == DeployPhase.ChoosingCell)
            {
                deployment.ChooseCell();
            }
            else if (deployment.Phase == DeployPhase.ChoosingFacing)
            {
                deployment.Confirm();
            }
        }

        private void OnCancelAction(InputAction.CallbackContext context)
        {
            // 配置操作中でなければ Esc は一時停止（T307）に回す。
            battle.Deployment?.Cancel();
        }

        private void Update()
        {
            var deployment = battle.Deployment;
            if (deployment == null)
            {
                return;
            }

            var pointer = _point.ReadValue<Vector2>();
            var pointerMoved = pointer != _lastPointer;
            _lastPointer = pointer;

            if (deployment.Phase != DeployPhase.ChoosingFacing)
            {
                _facingDrag = false;
            }

            // キーボードで動かしたカーソルを、止まっているマウスが上書きしないよう、動いたときだけ追う。
            if (deployment.Phase == DeployPhase.ChoosingCell && pointerMoved && TryGetCell(pointer, out var hovered))
            {
                deployment.SetCursor(hovered);
            }

            if (_press.WasPressedThisFrame() && deployment.Phase == DeployPhase.ChoosingFacing)
            {
                _facingDrag = true;
            }

            var dragged = false;
            if (_facingDrag && TryGetFacing(pointer, out var facing))
            {
                deployment.SetFacing(facing);
                dragged = true;
            }

            if (!_press.WasReleasedThisFrame())
            {
                return;
            }

            if (_facingDrag)
            {
                _facingDrag = false;
                if (dragged)
                {
                    deployment.Confirm();
                }
                else
                {
                    deployment.Cancel();
                }
            }
            else if (deployment.Phase == DeployPhase.ChoosingCell && TryGetCell(pointer, out var dropped))
            {
                deployment.SetCursor(dropped);
                deployment.ChooseCell();
            }
        }

        private bool TryGetCell(Vector2 screen, out GridPoint cell)
        {
            cell = default;
            return battleCamera.pixelRect.Contains(screen)
                && Physics.Raycast(battleCamera.ScreenPointToRay(screen), out var hit, 100f)
                && fieldView.TryGetCell(hit.collider.transform.position, out cell);
        }

        /// <summary>選んだマスからポインターへの向き（フィールドの水平面上）。近すぎれば false。</summary>
        private bool TryGetFacing(Vector2 screen, out Facing facing)
        {
            facing = default;
            var origin = fieldView.CellTopToWorld(battle.Deployment.Cursor);
            var plane = new Plane(fieldView.transform.up, origin);
            var ray = battleCamera.ScreenPointToRay(screen);
            if (!plane.Raycast(ray, out var distance))
            {
                return false;
            }

            var local = fieldView.transform.InverseTransformVector(ray.GetPoint(distance) - origin);
            if (new Vector2(local.x, local.z).magnitude < facingDragCells)
            {
                return false;
            }

            // フィールドの +z は画面の奥＝マップの上。
            facing = Mathf.Abs(local.x) >= Mathf.Abs(local.z)
                ? local.x > 0f ? Facing.Right : Facing.Left
                : local.z > 0f ? Facing.Up : Facing.Down;
            return true;
        }
    }
}
