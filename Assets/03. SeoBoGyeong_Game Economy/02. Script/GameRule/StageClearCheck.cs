using UnityEngine;
namespace LastJumpCrew.SeoBoGyeong.GameRule
{
    public class StageClearCheck:MonoBehaviour
    {
        //구성 : 생존인원 체크 / 함선 체력 체크/ 점프 활성화 체크 / 타임아웃 체크
        private int _playerCount;
        private int _deathCount;

        //테스트용
        private string _signal;

        private bool CheckAlivePlayer()
        {
            return _deathCount < _playerCount;
        }

        private void UpdateDeadSignal()
        {
            //사망 신호 받기
            if (_signal == "dead")
            {
                _deathCount++;
            }
            else if (_signal == "revive")
            {
                _deathCount--;
            }

            if (!CheckAlivePlayer())
            {
                //게임오버 이벤트 생성
            }
        }
    }
}

