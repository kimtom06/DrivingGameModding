#if MOD_INGAME
using Photon.Pun;
using UnityEngine;

[RequireComponent(typeof(PhotonView))]
public class PhotonModCarSync : MonoBehaviourPun
{
    [SerializeField] private ModLoader modLoader;

    private string lastReceivedHash = "";

    private void Awake()
    {
        if (!modLoader)
            modLoader = GetComponentInChildren<ModLoader>(true);
    }

    /// <summary>
    /// 내가 현재 사용하고 있는 모드의 hash를 다른 플레이어에게 보냄.
    /// ModLoader.Initialize()가 끝난 뒤 호출하는 것이 안전함.
    /// </summary>
   public void BroadcastCurrentMod()
{
    if (!PhotonNetwork.InRoom)
        return;

    if (!photonView.IsMine)
        return;

    if (!modLoader)
        return;

    string hash = modLoader.GetModHash();

    if (string.IsNullOrEmpty(hash))
        return;

    Debug.Log(
        $"[PhotonModCarSync] Send Mod Hash : {hash}"
    );

    // 기존 Buffered RPC 삭제
    PhotonNetwork.RemoveRPCs(photonView);

    // 현재 mod hash를 다시 buffer에 저장
    photonView.RPC(
        nameof(RPC_LoadModFromHash),
        RpcTarget.OthersBuffered,
        hash
    );
}

    [PunRPC]
    private void RPC_LoadModFromHash(string hash)
    {
        // 자기 자신의 차량에는 적용하지 않음.
        if (photonView.IsMine)
            return;

        if (string.IsNullOrEmpty(hash))
            return;

        // 같은 모드를 중복 로딩하지 않음.
        if (lastReceivedHash == hash)
            return;

        if (PlayerCarDataManager.instance == null)
        {
            Debug.LogWarning(
                "[PhotonModCarSync] CarDataManager.instance is null."
            );

            return;
        }

        int carID =
            PlayerCarDataManager.instance.GetModCarID(hash);

        if (carID == -1)
        {
            Debug.LogWarning(
                $"[PhotonModCarSync] Mod not installed. Hash : {hash}"
            );

            return;
        }

        if (!modLoader)
        {
            Debug.LogWarning(
                "[PhotonModCarSync] ModLoader not found."
            );

            return;
        }

        lastReceivedHash = hash;

        Debug.Log(
            $"[PhotonModCarSync] Loading remote mod. " +
            $"carID = {carID}, hash = {hash}"
        );

        modLoader.LoadModFromCarIndex(carID);
    }
}
#endif