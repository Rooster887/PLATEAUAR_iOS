using PlateauAR.Geospatial;
using PlateauToolkit.AR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace PlateauAR
{
    /// <summary>
    /// Controller for the settings of AR.
    /// </summary>
    public class ARSettingsController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] ARSettingsUI m_ARSettingsUI;

        [Header("AR Components")]
        [SerializeField] Material m_AROccluderMaterial;
        [SerializeField] PlateauARPositioning m_ARPositioning;

        PlateauARMarkerGroundController m_ARMarkerGroundController;
        Plateau3DTilePrefecture[] m_StreamingPrefectures;

        void Awake()
        {
            m_ARSettingsUI.ARGroundMarkerUI.ApplyButton.onClick.AddListener(() =>
            {
                if (m_ARMarkerGroundController == null)
                {
                    return;
                }

                if (!m_ARPositioning.GetOffset(out Vector3 offset))
                {
                    return;
                }
                offset.y = -m_ARMarkerGroundController.HeightGap;
                m_ARPositioning.SetOffset(offset);
                m_ARSettingsUI.OffsetInputUI.Value = offset;
            });

            m_ARSettingsUI.OffsetInputUI.OnApplied.AddListener(() =>
            {
                m_ARPositioning.SetOffset(m_ARSettingsUI.OffsetInputUI.Value);
            });
            m_ARSettingsUI.OffsetControllerUI.OnOffsetChanged += offsetDelta =>
            {
                if (!m_ARPositioning.GetOffset(out Vector3 offset))
                {
                    return;
                }
                offset += offsetDelta;
                m_ARPositioning.SetOffset(offset);
                m_ARSettingsUI.OffsetInputUI.Value = offset;
            };

            m_ARSettingsUI.MaterialDropdown.AddOptions(new List<TMPro.TMP_Dropdown.OptionData>
            {
                new ("ARオクルージョンシェーダー"),
            });

            m_ARSettingsUI.ColorInput.Value = m_AROccluderMaterial.color;
            m_ARSettingsUI.ColorInput.OnColorChanged += color =>
            {
                m_AROccluderMaterial.color = color;
            };

            m_ARSettingsUI.PrefectureDropdown.onValueChanged.AddListener(SelectPrefecture);
        }

        void Start()
        {
            // If not Cesium mode, disable the 3DTile selector UI
            bool isCesium = m_ARPositioning.PositioningType == PlateauARPositioningType.Cesium;
            m_ARSettingsUI.Set3DTileUIEnable(isCesium);

            // Get the AR marker controller
            m_ARPositioning.TryGetComponent(out m_ARMarkerGroundController);

            // Fetch the 3DTile list if the positioning mode is Cesium
            if (m_ARPositioning.PositioningType == PlateauARPositioningType.Cesium)
            {
                StartCoroutine(Fetch3DTilesUrls());
            }
        }

        void Update()
        {
            if (m_ARMarkerGroundController != null)
            {
                string groundMarkerInfo;
                bool isMarkerReady;
                switch (m_ARMarkerGroundController.MarkerState)
                {
                    case PlateauARGroundMarkerState.MarkerNotDetected:
                        groundMarkerInfo = "マーカー未検出";
                        isMarkerReady = false;
                        break;
                    case PlateauARGroundMarkerState.BuildingNotDetected:
                        groundMarkerInfo = "高さ調整基準点未検出";
                        isMarkerReady = false;
                        break;
                    case PlateauARGroundMarkerState.Detected:
                        groundMarkerInfo = $"マーカーと地面の差: {m_ARMarkerGroundController.HeightGap}[m]";
                        isMarkerReady = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                m_ARSettingsUI.ARGroundMarkerUI.ApplyButton.interactable = isMarkerReady;
                m_ARSettingsUI.ARGroundMarkerUI.ARGroundInfoText.text = groundMarkerInfo;
            }
        }

        IEnumerator Fetch3DTilesUrls()
        {
            if (m_StreamingPrefectures != null)
            {
                yield break;
            }

            Task<Plateau3DTilePrefecture[]> getPrefecturesTask = Plateau3DTileList.Get3DTilePrefectures();
            while (!getPrefecturesTask.IsCompleted)
            {
                yield return null;
            }

            if (!getPrefecturesTask.IsCompletedSuccessfully)
            {
                Debug.LogError("3DTileリストの取得に失敗しました");
                yield break;
            }

            m_StreamingPrefectures = getPrefecturesTask.Result;
            Debug.Assert(m_StreamingPrefectures != null);

            if (m_StreamingPrefectures.Length == 0)
            {
                m_ARSettingsUI.Set3DTileUIEnable(false);
                yield break;
            }

            m_ARSettingsUI.PrefectureDropdown.AddOptions(
                m_StreamingPrefectures.Select(p => new TMPro.TMP_Dropdown.OptionData(p.PrefectureName)).ToList());
            SelectPrefecture(0);
        }

        void SelectPrefecture(int prefectureIndex)
        {
            m_ARSettingsUI.StreamingUrlDropdown.ClearOptions();
            m_ARSettingsUI.StreamingUrlDropdown.AddOptions(
                m_StreamingPrefectures[prefectureIndex].Urls
                    .Select(entity => new TMPro.TMP_Dropdown.OptionData($"{entity.Name} (LOD{entity.Lod})", null)).ToList());

            m_ARSettingsUI.StreamingUrlDropdown.onValueChanged.RemoveAllListeners();
            m_ARSettingsUI.StreamingUrlDropdown.onValueChanged.AddListener(urlIndex =>
            {
                Plateau3DTile selectedTile = m_StreamingPrefectures[prefectureIndex].Urls[urlIndex];
                m_ARPositioning.Set3DTilesetUrl(selectedTile.Url);
            });
        }
    }
}