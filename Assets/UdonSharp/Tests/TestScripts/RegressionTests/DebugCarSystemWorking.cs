// Script from https://github.com/kurotori4423/KurotoriUdonKart modified by Occala that's a decent test for COW values leaking between scopes
// The line of interest is `steering = -MAX_STEERING_ANGLE * HandleMapping(velocity, 90, 0.2f);` 
// Prior to fixes for leaking of symbol values across scopes, this was overwriting temporary float values that shouldn't have been modified.
// Do not move anything in this file around at all. Any changes are likely to "fix" the issue this is looking for.

using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

public class DebugCarSystemWorking : UdonSharpBehaviour
{
    public WheelCollider leftFrontWheel;
    public WheelCollider rightFrontWheel;

    public WheelCollider leftRearWheel;
    public WheelCollider rightRearWheel;

    public const float MAX_SPEED = 30.0f;          // 最大スピード
    public const float MAX_MOTOR_TORQUE = 300;      // 最大モータートルク
    public const float MAX_STEERING_ANGLE = 30;     // 最大ステアリング角度
    public const float MAX_BRAKE_TORQUE = 500;      // 最大ブレーキトルク
    public const float CONST_BRAKE_TORQUE = 1.0f;   // 恒常的なブレーキ
    public const float DOWN_FORCE = 100.0f;           // ダウンフォースの強さ
    public const float CENTER_MASS_POS = 0.0f;      // 重心の低さ

    private float motor = 0;

    public float steering = 0;

    private float brake = 0;

    public float velocity;
    private Rigidbody rigidBody;

    // Clamp and control of steering element?
    float HandleMapping(float sp, float maxsp, float min)
    {
        return Mathf.Clamp((Mathf.Abs(sp) * (-min / maxsp) + 1.0f), min, 1.0f);
    }

    void Start()
    {
        rigidBody = GetComponent<Rigidbody>();
    }

    public void LateUpdate()
    {

        if (Networking.LocalPlayer == null)
        {
            CalcCarVelocity();
            ControllerEditorDebug();
            rigidBody.AddForce(0, Mathf.Abs(velocity) * DOWN_FORCE, 0);
            return;
        }
    }


    private void ControllerDesktop()
    {
        //brake = 0.0f;
        //steering = 0f;

        //if (Input.GetKey(KeyCode.S))
        //{
        //    if (velocity > 0.00001f)
        //    {
        //        motor = 0.0f;
        //        brake = MAX_BRAKE_TORQUE;
        //    }
        //    else
        //    {
        //        motor = -MAX_MOTOR_TORQUE;
        //    }
        //}
        //else if (Input.GetKey(KeyCode.W))
        //{
        //    if (velocity < -0.00001f)
        //    {
        //        motor = 0.0f;
        //        brake = MAX_BRAKE_TORQUE;
        //    }
        //    else
        //    {
        //        motor = MAX_MOTOR_TORQUE;
        //    }
        //}
        //else
        //{
        //    motor = CONST_BRAKE_TORQUE;
        //}


        if (Input.GetKey(KeyCode.A))
        {
            //Debug.Log(-1f);
            steering = -MAX_STEERING_ANGLE * HandleMapping(velocity, 90, 0.2f);
        }
        //else if (Input.GetKey(KeyCode.D))
        //{
        //    steering = MAX_STEERING_ANGLE * HandleMapping(velocity, 90, 0.2f);
        //}
        //else
        //{
        //    steering = 0;
        //}
    }

    private void CalcCarVelocity()
    {
        var direction = transform.forward;
        if (rigidBody.velocity.magnitude < 0.0001f)
        {
            velocity = 0.0f;
            return;
        }
        var sign = Mathf.Sign(Vector3.Dot(rigidBody.velocity.normalized, direction));
        velocity = Vector3.Project(rigidBody.velocity, direction).magnitude * sign;
    }

    private void ControllerEditorDebug()
    {
        // Editorモードでのデバッグ

        leftFrontWheel.brakeTorque = .0f;
        rightFrontWheel.brakeTorque = .0f;
        leftRearWheel.brakeTorque = .0f;
        rightRearWheel.brakeTorque = .0f;
        steering = 0f;

        ControllerDesktop();

        leftFrontWheel.steerAngle = steering;
        rightFrontWheel.steerAngle = steering;

        //leftRearWheel.motorTorque = motor;
        //rightRearWheel.motorTorque = motor;

        leftFrontWheel.motorTorque = motor;
        rightFrontWheel.motorTorque = motor;

        leftFrontWheel.brakeTorque = brake;
        rightFrontWheel.brakeTorque = brake;
        leftRearWheel.brakeTorque = brake;
        rightRearWheel.brakeTorque = brake;
    }
}
