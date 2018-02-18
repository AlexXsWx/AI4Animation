﻿using System;
using System.Collections;
using UnityEngine;

public class FootIK : MonoBehaviour {

	public bool AutoUpdate = true;
	public int Iterations = 5;
	public LayerMask Ground;
	public Vector3 Goal;

	[Range(0f, 1f)] public float Step = 1.0f;
	[Range(0f, 1f)] public float Damping = 0.1f;

	public Transform[] Transforms;

	private int Bones;
	private int DOF;
	private int Dimensions;
	private Matrix Jacobian;
	private Matrix Gradient;

	private float Differential = 0.01f;

	void Reset() {
		Transforms = new Transform[1] {transform};
	}

	void LateUpdate() {
		if(AutoUpdate) {
			ComputeGoal();
			ProcessIK();
		}
	}

	public void ComputeGoal() {
		Goal = GetTipPosition();
		RaycastHit[] upHits = Physics.RaycastAll(Goal+Vector3.down, Vector3.up, Ground);
		RaycastHit[] downHits = Physics.RaycastAll(Goal+Vector3.up, Vector3.down, Ground);
		if(upHits.Length != 0 || downHits.Length != 0) {
			float height = float.MinValue;
			for(int i=0; i<downHits.Length; i++) {
				if(downHits[i].point.y > height && !downHits[i].collider.isTrigger) {
					height = downHits[i].point.y;
				}
			}
			for(int i=0; i<upHits.Length; i++) {
				if(upHits[i].point.y > height && !upHits[i].collider.isTrigger) {
					height = upHits[i].point.y;
				}
			}
			Goal.y = Mathf.Max(Goal.y, height);
		}
	}

	public void ProcessIK() {
		if(Transforms.Length == 0) {
			return;
		}

		Bones = Transforms.Length;
		DOF = Bones * 3;
		Dimensions = 3;

		Matrix4x4[] posture = GetPosture();
		float[] solution = new float[DOF];
		Jacobian = new Matrix(Dimensions, DOF);
		Gradient = new Matrix(Dimensions, 1);

		for(int i=0; i<Iterations; i++) {
			Iterate(posture, solution);
		}

		FK(posture, solution);
	}
	
	public Vector3 GetTipPosition() {
		return Transforms[Transforms.Length-1].position;
	}

	private void FK(Matrix4x4[] posture, float[] variables) {
		for(int i=0; i<Bones; i++) {
			Quaternion update = Quaternion.AngleAxis(Mathf.Rad2Deg*variables[i*3+0], Vector3.forward) * Quaternion.AngleAxis(Mathf.Rad2Deg*variables[i*3+1], Vector3.right) * Quaternion.AngleAxis(Mathf.Rad2Deg*variables[i*3+2], Vector3.up);
			Transforms[i].localPosition = posture[i].GetPosition();
			Transforms[i].localRotation = posture[i].GetRotation() * update;
		}
	}

	private Matrix4x4[] GetPosture() {
		Matrix4x4[] posture = new Matrix4x4[Bones];
		for(int i=0; i<Bones; i++) {
			posture[i] = Transforms[i].GetLocalMatrix();
		}
		return posture;
	}

	private void Iterate(Matrix4x4[] posture, float[] variables) {
		FK(posture, variables);
		Vector3 tipPosition = GetTipPosition();

		//Jacobian
		for(int j=0; j<DOF; j++) {
			variables[j] += Differential;
			FK(posture, variables);
			variables[j] -= Differential;

			Vector3 deltaPosition = (GetTipPosition() - tipPosition) / Differential;
			Jacobian.Values[0][j] = deltaPosition.x;
			Jacobian.Values[1][j] = deltaPosition.y;
			Jacobian.Values[2][j] = deltaPosition.z;
		}

		//Gradient Vector
		Vector3 gradientPosition = Step * (Goal - tipPosition);
		Gradient.Values[0][0] = gradientPosition.x;
		Gradient.Values[1][0] = gradientPosition.y;
		Gradient.Values[2][0] = gradientPosition.z;

		//Jacobian Damped-Least-Squares
		Matrix DLS = DampedLeastSquares();
		for(int m=0; m<DOF; m++) {
			for(int n=0; n<Dimensions; n++) {
				variables[m] += DLS.Values[m][n] * Gradient.Values[n][0];
			}
		}
	}

	private Matrix DampedLeastSquares() {
		Matrix transpose = new Matrix(DOF, Dimensions);
		for(int m=0; m<Dimensions; m++) {
			for(int n=0; n<DOF; n++) {
				transpose.Values[n][m] = Jacobian.Values[m][n];
			}
		}
		Matrix jTj = transpose * Jacobian;
		for(int i=0; i<DOF; i++) {
			jTj.Values[i][i] += Damping*Damping;
		}
		Matrix dls = jTj.GetInverse() * transpose;
		return dls;
  	}

	void OnDrawGizmos() {
		if(Transforms.Length == 0) {
			return;
		}
		for(int i=0; i<Transforms.Length-1; i++) {
			Gizmos.color = Color.cyan;
			Gizmos.DrawSphere(Transforms[i].position, 0.01f);
			Gizmos.color = Color.yellow;
			Gizmos.DrawLine(Transforms[i].position, Transforms[i+1].position);
		}
			Gizmos.color = Color.cyan;
			Gizmos.DrawSphere(Transforms[Transforms.Length-1].position, 0.01f);
	}

}