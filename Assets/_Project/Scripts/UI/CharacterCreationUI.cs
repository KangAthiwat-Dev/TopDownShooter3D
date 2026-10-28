using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Only the existing HumanMale skin, hair colours and three styles are offered.</summary>
public sealed class CharacterCreationUI : MonoBehaviour
{
    [SerializeField] private GameObject window,previewPrefab;
    [SerializeField] private RawImage portrait;
    [SerializeField] private GameMenuUI menu;
    [SerializeField] private Button confirm,cancel,defaults,rotateLeft,rotateRight;
    [SerializeField] private TMP_Text confirmLabel;
    [SerializeField] private Button[] skinButtons,hairButtons,styleButtons;
    [SerializeField] private Color[] skinColors,hairColors;
    private GameObject stage,actor;
    private Camera previewCamera;
    private RenderTexture texture;
    private PlayerAppearance appearance;
    private bool beginAfterSave;
    public bool IsOpen=>window!=null&&window.activeSelf;
    public PlayerAppearance PreviewAppearance=>appearance;
    public bool HasSavedAppearance
    {
        get
        {
            if(!PlayerAppearanceProfile.TryRead(out var saved))return false;
            var model=previewPrefab.GetComponentInChildren<PlayerAppearance>(true);
            for(int i=0;i<model.HairStyleCount;i++)if(model.GetHairStyleId(i)==saved.hairStyleId)return true;
            return false;
        }
    }
    private void Awake()
    {
        for(int i=0;i<skinButtons.Length;i++){int index=i;skinButtons[i].onClick.AddListener(()=>SelectSkin(index));}
        for(int i=0;i<hairButtons.Length;i++){int index=i;hairButtons[i].onClick.AddListener(()=>SelectHair(index));}
        for(int i=0;i<styleButtons.Length;i++){int index=i;styleButtons[i].onClick.AddListener(()=>SelectStyle(index));}
        confirm.onClick.AddListener(Confirm);cancel.onClick.AddListener(Cancel);
        defaults.onClick.AddListener(()=>{appearance.ApplyAppearance(PlayerAppearanceData.Default);RefreshSelection();});
        rotateLeft.onClick.AddListener(()=>actor.transform.Rotate(0,-25,0,Space.World));rotateRight.onClick.AddListener(()=>actor.transform.Rotate(0,25,0,Space.World));
        window.SetActive(false);
    }
    public void Open(bool playAfterSave)
    {
        EnsurePreview();beginAfterSave=playAfterSave;
        if(!PlayerAppearanceProfile.TryRead(out var saved)||!appearance.ApplyAppearance(saved))appearance.ApplyAppearance(PlayerAppearanceData.Default);
        actor.transform.localRotation=Quaternion.identity;RefreshSelection();
        confirmLabel.text=playAfterSave?"ยืนยันและเข้าเกม":"บันทึกรูปลักษณ์";
        window.SetActive(true);stage.SetActive(true);transform.SetAsLastSibling();
        EventSystem.current?.SetSelectedGameObject(confirm.gameObject);
    }
    public void SelectSkin(int index){if(appearance==null||index<0||index>=skinColors.Length)return;appearance.SetSkinColor(skinColors[index]);RefreshSelection();}
    public void SelectHair(int index){if(appearance==null||index<0||index>=hairColors.Length)return;appearance.SetHairColor(hairColors[index]);RefreshSelection();}
    public void SelectStyle(int index){if(appearance==null||index<0||index>=appearance.HairStyleCount)return;appearance.TrySetHairStyle(appearance.GetHairStyleId(index));RefreshSelection();}
    public void Confirm()
    {
        if(!IsOpen)return;PlayerAppearanceProfile.Save(appearance.CurrentAppearance);Close();
        if(beginAfterSave)menu.StartAdventureAfterCustomization();else menu.Back();
    }
    public void Cancel(){if(!IsOpen)return;Close();menu.Back();}
    private void Close(){window.SetActive(false);if(stage!=null)stage.SetActive(false);}
    private void RefreshSelection()
    {
        var data=appearance.CurrentAppearance;
        for(int i=0;i<skinButtons.Length;i++)skinButtons[i].GetComponent<Outline>().enabled=Near(data.skinColor,skinColors[i]);
        for(int i=0;i<hairButtons.Length;i++)hairButtons[i].GetComponent<Outline>().enabled=Near(data.hairColor,hairColors[i]);
        for(int i=0;i<styleButtons.Length;i++)styleButtons[i].GetComponent<Outline>().enabled=data.hairStyleId==appearance.GetHairStyleId(i);
    }
    private static bool Near(Color a,Color b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)<.02f;
    private void EnsurePreview()
    {
        if(stage!=null)return;
        stage=new GameObject("Character creator preview stage");stage.transform.position=new Vector3(2000,2000,2000);
        actor=Instantiate(previewPrefab,stage.transform);actor.transform.localPosition=Vector3.zero;actor.transform.localRotation=Quaternion.identity;
        foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
        appearance=actor.GetComponentInChildren<PlayerAppearance>(true);
        var animator=actor.GetComponentInChildren<Animator>();
        if(animator!=null){animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;for(int i=1;i<animator.layerCount;i++)animator.SetLayerWeight(i,0);animator.Update(0);}
        var renderers=actor.GetComponentsInChildren<SkinnedMeshRenderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        texture=new RenderTexture(640,800,24);texture.name="Character creation portrait";texture.Create();portrait.texture=texture;
        var cameraObject=new GameObject("Portrait camera");cameraObject.transform.SetParent(stage.transform,false);previewCamera=cameraObject.AddComponent<Camera>();
        previewCamera.targetTexture=texture;previewCamera.cullingMask=1<<31;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.backgroundColor=new Color(.035f,.06f,.055f);previewCamera.fieldOfView=30;
        previewCamera.nearClipPlane=.05f;previewCamera.farClipPlane=30;
        float distance=Mathf.Max(bounds.size.y,bounds.size.x)*2.25f;
        previewCamera.transform.position=bounds.center+new Vector3(0,bounds.size.y*.04f,-distance);previewCamera.transform.LookAt(bounds.center);
        Light("Portrait key",new Vector3(35,-35,0),1.2f);Light("Portrait fill",new Vector3(20,140,0),.65f);
    }
    private void Light(string name,Vector3 rotation,float intensity)
    {
        var go=new GameObject(name);go.transform.SetParent(stage.transform,false);go.transform.localRotation=Quaternion.Euler(rotation);
        var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=intensity;light.cullingMask=1<<31;light.shadows=LightShadows.None;
    }
    private void OnDestroy(){if(stage!=null)Destroy(stage);if(texture!=null){texture.Release();Destroy(texture);}}
}
