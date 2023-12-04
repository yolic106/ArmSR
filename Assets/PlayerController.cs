using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    float h;
    float v;
    Rigidbody rb;
    public int speed;

    // Start is called before the first frame update
    void Start()
    {
        rb = this.GetComponent<Rigidbody>(); //获取这个物件下挂载的Rigidbody为rb赋值
    }

    // Update is called once per frame
    void Update()
    {
        h = Input.GetAxis("Horizontal");
        v = Input.GetAxis("Vertical");
    }
    private void FixedUpdate()
    {
        Vector3 force =new Vector3(h,0,v);
        force *= speed;
        rb.AddForce(force);
    }
}
