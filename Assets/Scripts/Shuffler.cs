using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public static class Shuffler {

	public static void ShuffleList<T>(List<T> list) {

		for (int i = 0; i < list.Count; i++) {
			int j = Random.Range (i, list.Count);
			if (j != i) {
				T t = list [i];
				list [i] = list [j];
				list [j] = t;
			}
		}
	}
}
