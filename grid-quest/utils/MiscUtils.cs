namespace GridQuest.Utils;

public static class MiscUtils
{
	public static bool isCheckerboard(int x, int y)
	{
		return (x + y) % 2 == 0;
	}
}
