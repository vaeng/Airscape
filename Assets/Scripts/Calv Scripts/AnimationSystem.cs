using System.Collections;
using UnityEngine.Animations.Rigging;
using UnityEngine;

public class AnimationSystem : MonoBehaviour
{
    [SerializeField] private Transform leftHandIK, rightHandIK, leftHandRestPos, rightHandRestPos, leftLeg, rightLeg;
    [SerializeField] private GameObject leftShoulder, rightShoulder;
    [SerializeField] private TwoBoneIKConstraint leftHandIKConstraint, rightHandIKConstraint;
    [SerializeField] private float handAnimationSpeed = 0.3f, legAnimationSpeed = 0.3f, legMaxAngle = 45f;

    private float lastLegPos;
    private bool isLegMoving = false;

    //Debug
    [SerializeField] private Transform leftMoney, rightMoney;
    [SerializeField] private bool dosomething, isGrabbing = false;


    private void OnEnable()
    {

        Debug.Log("AnimationSystem OnEnable called.");

        if (leftShoulder == null || rightShoulder == null)
        {
            Debug.LogError("Left or Right Shoulder GameObject is not assigned in the AnimationSystem script.");
            return;
        }

        if (leftHandIKConstraint == null)
        {
            leftHandIKConstraint = leftShoulder.GetComponent<TwoBoneIKConstraint>();
        }

        Debug.Log(leftHandIKConstraint == null ? "Left Hand IK Constraint is null" : "Left Hand IK Constraint is not null");

        if (rightHandIKConstraint == null)
        {
            rightHandIKConstraint = rightShoulder.GetComponent<TwoBoneIKConstraint>();
        }

        Debug.Log(rightHandIKConstraint == null ? "Right Hand IK Constraint is null" : "Right Hand IK Constraint is not null");

        if (leftHandIK == null || rightHandIK == null || leftHandRestPos == null || rightHandRestPos == null)
        {
            Debug.LogError("One or more IK Transforms are not assigned in the AnimationSystem script.");
            return;
        }

        Release();
    }


    // DEBUGGING PURPOSES ONLY

    //private void Update()
    //{
    //    if (dosomething)
    //    {
    //        dosomething = false;

    //        if (!isGrabbing)
    //        {
    //            Grab(leftMoney, rightMoney);
    //            isGrabbing = true;
    //        }
    //        else
    //        {
    //            Release();
    //            isGrabbing = false;
    //        }

    //    }
    //}


    private void Update()
    {
        if (isLegMoving)
        {
            float legAngle = Mathf.Sin(Time.time * legAnimationSpeed) * legMaxAngle;
            leftLeg.localRotation = Quaternion.Euler(legAngle, 0f, 0f);
            rightLeg.localRotation = Quaternion.Euler(0f, 0f, legAngle);
        }
    }

    public void StartMovementAnimation()
    {
        lastLegPos = 0f;
        isLegMoving = true;
    }

    public void StopMovementAnimation()
    {
        isLegMoving = false;
        leftLeg.localRotation = Quaternion.identity;
        rightLeg.localRotation = Quaternion.identity;
    }

    public void Grab(Transform leftHandTarget = null, Transform rightHandTarget = null)
    {
        if (leftHandTarget != null)
        {
            leftHandIK.position = leftHandTarget.position;
            leftHandIK.rotation = leftHandTarget.rotation;
            leftHandIK.parent = leftHandTarget;

            if (leftHandIKConstraint == null)
            {
                Debug.LogError("Left Hand IK Constraint is not assigned in the AnimationSystem script.");
            }
            else
            {
                StartCoroutine(AnimateIKWeight(leftHandIKConstraint, 1f));
            }

        }

        if (rightHandTarget != null)
        {

            rightHandIK.position = rightHandTarget.position;
            rightHandIK.rotation = rightHandTarget.rotation;
            rightHandIK.parent = rightHandTarget;

            if (rightHandIKConstraint == null)
            {
                Debug.LogError("Right Hand IK Constraint is not assigned in the AnimationSystem script.");
            }
            else
            {
                StartCoroutine(AnimateIKWeight(rightHandIKConstraint, 1f));
            }

        }

    }

    public void Release()
    {
        leftHandIK.position = leftHandRestPos.position;
        leftHandIK.parent = leftHandRestPos;

        rightHandIK.position = rightHandRestPos.position;
        rightHandIK.parent = rightHandRestPos;

        leftHandIK.rotation = leftHandRestPos.rotation;
        rightHandIK.rotation = rightHandRestPos.rotation;

        StartCoroutine(AnimateIKWeight(leftHandIKConstraint, 0f));
        StartCoroutine(AnimateIKWeight(rightHandIKConstraint, 0f));
    }

    private IEnumerator AnimateIKWeight(TwoBoneIKConstraint ikConstraint, float targetWeight)
    {
        float currentWeight = ikConstraint.weight;
        float elapsedTime = 0f;

        while (elapsedTime < handAnimationSpeed)
        {
            elapsedTime += Time.deltaTime;
            ikConstraint.weight = Mathf.Lerp(currentWeight, targetWeight, elapsedTime / handAnimationSpeed);
            yield return null;
        }

        ikConstraint.weight = targetWeight;
    }
}
