using UnityEngine;

namespace UTJ {

public struct InputBuffer
{
	public const int InputMax = 8;
	public int[] Buttons;
	public bool[] Touched;
	public Vector2[] TouchedPosition;
}

public class InputManager
{
	// singleton
	static InputManager _instance;
	public static InputManager Instance => _instance ?? (_instance = new InputManager());

	public const int One = 4096;
	public const float InvOne = 1f/((float)One);

	public enum Button {
		Horizontal,
		Vertical,
		Fire,
	}

	public InputBuffer InputBuffer;
	public void Init()
	{
		InputBuffer = new InputBuffer();
		InputBuffer.Buttons = new int[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, };
		InputBuffer.Touched = new bool[2] { false, false };
		InputBuffer.TouchedPosition = new Vector2[2] { Vector2.zero, Vector2.zero, };
	}

	public int GetButton(Button button)
	{
		return InputBuffer.Buttons[(int)button];
	}
	public bool IsButton(Button button)
	{
		return InputBuffer.Buttons[(int)button] != 0;
	}
	public float GetAnalog(Button button)
	{
		// Debug.Log((float)(input_buffer_.buttons_[(int)button]) * INV_ONE);
		return (float)(InputBuffer.Buttons[(int)button]) * InvOne;
	}
	public bool Touched(int index)
	{
		return InputBuffer.Touched[index];
	}
	public Vector2 GetTouchedPosition(int index)
	{
		return InputBuffer.TouchedPosition[index];
	}

	private void set_buttons()
	{
		int[] buttons = InputBuffer.Buttons;
		buttons[(int)InputManager.Button.Horizontal] = (int)(Input.GetAxisRaw("Horizontal") * InputManager.One);
		buttons[(int)InputManager.Button.Vertical] = (int)(Input.GetAxisRaw("Vertical") * InputManager.One);
		buttons[(int)InputManager.Button.Fire] = (Input.GetButton("Fire1") ? 1 : 0);
	}
	private void set_touched(bool touched, in Vector2 pos, int index)
	{
		InputBuffer.Touched[index] = touched;
		InputBuffer.TouchedPosition[index] = pos;
	}

	public void Update()
	{
		set_buttons();

		bool clicked0 = false;
		bool clicked1 = false;
		var clickedPosition0 = new Vector2(0f, 0f);
		var clickedPosition1 = new Vector2(0f, 0f);
		if (Input.touchCount > 0) {
			clickedPosition0 = Input.GetTouch(0).position;
			clicked0 = true;
			if (Input.touchCount > 1) {
				clickedPosition1 = Input.GetTouch(1).position;
				clicked1 = true;
			}
		} else if (Input.GetMouseButton(0)) {
			clickedPosition0 = Input.mousePosition;
			clicked0 = true;
		}
		clickedPosition0.x -= Screen.width*0.5f;
		clickedPosition0.y -= Screen.height*0.5f;
		clickedPosition1.x -= Screen.width*0.5f;
		clickedPosition1.y -= Screen.height*0.5f;
		set_touched(clicked0, in clickedPosition0, 0 /* index */);
		set_touched(clicked1, in clickedPosition1, 1 /* index */);
	}
}

} // namespace UTJ {
