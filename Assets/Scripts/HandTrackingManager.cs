
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;

public class HandTrackingManager : MonoBehaviour
{
    private List<XRHandSubsystem> handSubsystems =
        new List<XRHandSubsystem>();

    [SerializeField] private float pinchThreshold = 0.025f;

    void Update()
    {
        SubsystemManager.GetSubsystems(handSubsystems);

        foreach (XRHandSubsystem subsystem in handSubsystems)
        {
            if (!subsystem.running)
                continue;

            CheckHand(subsystem.leftHand, "Left");
            CheckHand(subsystem.rightHand, "Right");
        }
    }

    void CheckHand(XRHand hand, string handName)
    {
        if (!hand.isTracked)
            return;

        XRHandJoint thumb =
            hand.GetJoint(XRHandJointID.ThumbTip);

        XRHandJoint index =
            hand.GetJoint(XRHandJointID.IndexTip);

        if (!thumb.TryGetPose(out Pose thumbPose))
            return;

        if (!index.TryGetPose(out Pose indexPose))
            return;

        float distance = Vector3.Distance(
            thumbPose.position,
            indexPose.position
        );

        bool isPinching = distance < pinchThreshold;

        if (isPinching)
        {
            Debug.Log(handName + " hand: PINCH");
        }
    }
}