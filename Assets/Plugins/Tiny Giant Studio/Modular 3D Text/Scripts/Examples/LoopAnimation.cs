using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TinyGiantStudio.Text.Example
{
    [RequireComponent(typeof(Modular3DText))]
    [AddComponentMenu("Tiny Giant Studio/Modular 3D Text/Extra/Loop Animation", order: 20102)]
    public class LoopAnimation : MonoBehaviour
    {
        [SerializeField] Vector2 duration = new Vector2(1, 2);
        [SerializeField] TargetType targetType = TargetType.letters;
        [Space]
        [SerializeField] Material focusedMaterial = null;

        //[HideInInspector]
        public List<GameObject> targetLetterList = new List<GameObject>();
        public readonly List<List<GameObject>> TargetWordsList = new List<List<GameObject>>();
        Modular3DText Modular3DText => GetComponent<Modular3DText>();


        private enum TargetType
        {
            letters,
            words
        }

        private void OnEnable()
        {
            UpdateTargetList();

            if (targetType == TargetType.letters)
                StartCoroutine(LetterAnimationRoutine());
            else
                StartCoroutine(WordAnimationRoutine());
        }

        /// <summary>
        /// This needs to be called everytime the text is changed
        /// </summary>
        public void UpdateTargetList()
        {
            targetLetterList.Clear();
            TargetWordsList.Clear();

            if (targetType == TargetType.letters)
            {
                for (int i = 0; i < Modular3DText.characterObjectList.Count; i++)
                {
                    targetLetterList.Add(Modular3DText.characterObjectList[i]);
                }
            }
            else
            {
                List<GameObject> letterList = new List<GameObject>();
                for (int i = 0; i < Modular3DText.characterObjectList.Count; i++)
                {
                    if (Modular3DText.characterObjectList[i].name == "Space")
                    {
                        TargetWordsList.Add(letterList);
                        letterList = new List<GameObject>();
                    }
                    else
                    {
                        letterList.Add(Modular3DText.characterObjectList[i]);
                    }
                }
                if (letterList.Count > 0)
                {
                    TargetWordsList.Add(letterList);
                }
            }
        }

        private IEnumerator LetterAnimationRoutine()
        {
            yield return null;
            for (int i = 0; i < targetLetterList.Count; i++)
            {
                Focus(targetLetterList[i]);
                yield return new WaitForSeconds(Random.Range(duration.x, duration.y));
                UnFocus(targetLetterList[i]);
            }

            StartCoroutine(LetterAnimationRoutine());
        }
        private IEnumerator WordAnimationRoutine()
        {
            yield return null;
            for (int i = 0; i < TargetWordsList.Count; i++)
            {
                for (int j = 0; j < TargetWordsList[i].Count; j++)
                {
                    Focus(TargetWordsList[i][j]);
                }

                yield return new WaitForSeconds(Random.Range(duration.x, duration.y));

                for (int j = 0; j < TargetWordsList[i].Count; j++)
                {
                    UnFocus(TargetWordsList[i][j]);
                }
            }

            StartCoroutine(WordAnimationRoutine());
        }


        private void Focus(GameObject target)
        {
            if (!target)
                return;

            if (focusedMaterial)
            {
                if (target.GetComponent<Renderer>())
                    target.GetComponent<Renderer>().material = focusedMaterial;
            }
        }
        private void UnFocus(GameObject target)
        {
            if (!target)
                return;

            if (target.GetComponent<Renderer>())
                target.GetComponent<Renderer>().material = Modular3DText.Material;

        }
    }
}