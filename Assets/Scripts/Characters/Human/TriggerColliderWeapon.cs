using Assets.Scripts;
using Assets.Scripts.Characters.Titan;
using Assets.Scripts.Gamemode.Options;
using Assets.Scripts.Services;
using Assets.Scripts.Settings;
using Assets.Scripts.Events.Args;
using System.Collections;
using UnityEngine;

public class TriggerColliderWeapon : MonoBehaviour
{
    public Equipment Equipment { get; set; }

    public bool IsActive;
    public bool RightHand;
    public IN_GAME_MAIN_CAMERA currentCamera;

    //Move this to Hero.cs after rewrite is made so events don't trigger per weapon.
    public ArrayList currentHits = new ArrayList();
    public ArrayList currentHitsII = new ArrayList();

    public AudioSource meatDie;
    public Hero hero;
    public int myTeam = 1;
    public float scoreMulti = 1f;
    public Rigidbody body;

    private FengGameManagerMKII manager;

    public void ClearHits()
    {
        currentHitsII.Clear();
        currentHits.Clear();
    }

    private void HeroHit(Hero hero, HitBox hitbox, float distance)
    {
        if (hero.myTeam != myTeam && !hero.isInvincible() && hero.HasDied() && !hero.isGrabbed)
        {
            // I honestly don't have a clue as to what this does
            float b = Mathf.Min(1f, 1f - (distance * 0.05f));

            hero.markDie();
            hero.photonView.RPC(nameof(Hero.netDie), PhotonTargets.All, new object[]
            {
                ((hitbox.transform.root.position - transform.position.normalized * b) * 1000f) + (Vector3.up * 50f),
                false,
                transform.root.gameObject.GetPhotonView().viewID,
                PhotonView.Find(transform.root.gameObject.GetPhotonView().viewID).owner.CustomProperties[PhotonPlayerProperty.name],
                false
            });
        }
    }

    private void OnTriggerStay(Collider collider)
    {
        if (!IsActive) return;

        if (!currentHitsII.Contains(collider.gameObject))
        {
            currentHitsII.Add(collider.gameObject);
            currentCamera.startShake(0.1f, 0.1f, 0.95f);
            if (collider.gameObject.transform.root.gameObject.CompareTag("titan"))
            {
                GameObject meat;
                hero.slashHit.Play();
                meat = PhotonNetwork.Instantiate("hitMeat", transform.position, Quaternion.Euler(270f, 0f, 0f), 0);
                meat.transform.position = transform.position;
                Equipment.Weapon.Use(0);
            }
        }

        if (currentHits.Contains(collider.gameObject))
            return;

        switch (collider.gameObject.tag)
        {
            case "playerHitbox":
                if (GameSettings.PvP.Mode != PvpMode.Disabled && collider.gameObject.TryGetComponent(out HitBox hitbox))
                {
                    if (hitbox.transform.root != null && hitbox.transform.root.TryGetComponent(out Hero attackedHero))
                    {
                        Service.Player.HeroHit(new HeroKillEvent(attackedHero, hero));

                        HeroHit(attackedHero, hitbox, Vector3.Distance(collider.gameObject.transform.position, transform.position));
                    }
                }
                break;
            case "titanneck":
                if (collider.gameObject.TryGetComponent(out HitBox hitBox) && hitBox.transform.root.TryGetComponent(out TitanBase titanBase))
                {

                    if (Vector3.Angle(-titanBase.Body.Head.forward, titanBase.Body.Head.position - titanBase.Body.Head.position) >= 70f)
                        break;

                    Vector3 velocity = body.velocity - hitBox.transform.root.GetComponent<Rigidbody>().velocity;
                    int damage = Mathf.Max(10, (int) ((velocity.magnitude * 10f) * scoreMulti));

                    Service.Player.TitanDamaged(new TitanDamagedEvent(titanBase, hero, damage));
                    Service.Player.TitanHit(new TitanHitEvent(titanBase, BodyPart.Nape, hero, RightHand));

                    titanBase.photonView.RPC(nameof(TitanBase.OnNapeHitRpc2), titanBase.photonView.owner, transform.root.gameObject.GetPhotonView().viewID, damage);
                }
                break;
            case "titaneye":
                {
                    currentHits.Add(collider.gameObject);
                    GameObject rootObject = collider.gameObject.transform.root.gameObject;

                    if (rootObject.TryGetComponent(out TitanBase titan))
                    {
                        Service.Player.TitanHit(new TitanHitEvent(titan, BodyPart.Eyes, hero, RightHand));


                        Vector3 velocity = body.velocity - rootObject.GetComponent<Rigidbody>().velocity;
                        int damage = Mathf.Max(10, (int) ((velocity.magnitude * 10f) * scoreMulti));

                        if (PhotonNetwork.isMasterClient)
                        {
                            titan.OnEyeHitRpc(transform.root.gameObject.GetPhotonView().viewID, damage);
                        }
                        else
                        {
                            titan.photonView.RPC(nameof(TitanBase.OnEyeHitRpc), titan.photonView.owner, transform.root.gameObject.GetPhotonView().viewID, damage);
                        }
                        ShowCriticalHitFX();
                    }
                }
                break;
            case "titanbodypart":
                {
                    currentHits.Add(collider.gameObject);
                    GameObject rootObject = collider.gameObject.transform.root.gameObject;

                    if (rootObject.TryGetComponent(out MindlessTitan mindlessTitan))
                    {
                        Vector3 velocity = this.body.velocity - rootObject.GetComponent<Rigidbody>().velocity;
                        int damage = Mathf.Max(10, (int) ((velocity.magnitude * 10f) * scoreMulti));
                        BodyPart body = mindlessTitan.Body.GetBodyPart(collider.transform);

                        Service.Player.TitanHit(new TitanHitEvent(mindlessTitan, body, hero, RightHand));
                        if (PhotonNetwork.isMasterClient)
                        {
                            mindlessTitan.OnBodyPartHitRpc(body, damage);
                        }
                        else
                        {
                            mindlessTitan.photonView.RPC(nameof(MindlessTitan.OnBodyPartHitRpc), mindlessTitan.photonView.owner, body, damage);
                        }
                    }
                }
                break;
            case "titanankle":
                {
                    currentHits.Add(collider.gameObject);
                    GameObject rootObj = collider.gameObject.transform.root.gameObject;
                    Vector3 velocity = Vector3.zero;

                    if (rootObj.TryGetComponent(out Rigidbody rigidbody))//patch for dummy titan
                    {
                        velocity = body.velocity - rigidbody.velocity;
                    }

                    var damage = Mathf.Max(10, (int) ((velocity.magnitude * 10f) * scoreMulti));

                    if (rootObj.TryGetComponent(out TitanBase titan))
                    {
                        Service.Player.TitanHit(new TitanHitEvent(titan, BodyPart.Ankle, hero, RightHand));
                        ShowCriticalHitFX();

                        if (!titan.IsAlive) return;

                        var infoArray = new object[] { damage, collider.gameObject.name == "ankleR" };
                        titan.photonView.RPC(nameof(TitanBase.OnAnkleHitRpc), PhotonTargets.MasterClient, infoArray);
                    }
                    else if (rootObj.TryGetComponent(out DummyTitan dummyTitan))
                    {
                        ShowCriticalHitFX();
                    }
                }
                break;
            default:
                break;
        }
    }

    private void ShowCriticalHitFX()
    {
        GameObject obj2 = PhotonNetwork.Instantiate("redCross", transform.position, Quaternion.Euler(270f, 0f, 0f), 0);
        currentCamera.startShake(0.2f, 0.3f, 0.95f);
        obj2.transform.position = transform.position;
    }

    private void Start()
    {
        currentCamera = GameObject.Find("MainCamera").GetComponent<IN_GAME_MAIN_CAMERA>();
        body = currentCamera.main_object.GetComponent<Rigidbody>();
        Equipment = transform.root.GetComponent<Equipment>();
        hero = currentCamera.main_object.GetComponent<Hero>();
        manager = GameObject.Find("MultiplayerManager").GetComponent<FengGameManagerMKII>();
    }
}