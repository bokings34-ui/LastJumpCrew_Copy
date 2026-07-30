using UnityEngine;

public class PlanetGravityField : MonoBehaviour
{
    public float gravityStrength = 9.81f; // 끌어당기는 힘

    // 중력장(Trigger) 안에 어떤 물체가 머물고 있을 때 매 프레임 실행됩니다.
    void OnTriggerStay(Collider other)
    {
        // 들어온 물체가 CharacterController를 가지고 있는지 확인합니다.
        CharacterController cc = other.GetComponent<CharacterController>();

        // CharacterController를 가진 녀석(예: 플레이어)이라면 중력을 적용합니다.
        if (cc != null)
        {
            // 1. 방향 계산: 대상의 위치에서 행성의 중심(나의 위치)을 뺀 후, 방향만 남깁니다.
            Vector3 gravityDirection = (transform.position - other.transform.position).normalized;

            // 2. 끌어당기기: 대상을 행성 중심 방향으로 이동시킵니다.
            cc.Move(gravityDirection * gravityStrength * Time.deltaTime);

            // 3. 발을 땅에 맞게 회전시키기 (아래에서 원리 설명)
            Vector3 gravityUp = -gravityDirection; // 행성에서 바깥으로 뻗어나가는 방향(위쪽)

            Quaternion targetRotation = Quaternion.FromToRotation(other.transform.up, gravityUp) * other.transform.rotation;

            // 대상의 몸을 부드럽게 회전시킵니다.
            other.transform.rotation = Quaternion.Slerp(other.transform.rotation, targetRotation, 50f * Time.deltaTime);
        }
    }
}