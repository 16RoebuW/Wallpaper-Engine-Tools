extends Node

func animated_texture_from_buffer(data: PackedByteArray, max_frames: int = 0) -> AnimatedTexture:
	return GifManager.animated_texture_from_buffer(data, max_frames)

func animated_texture_from_file(path: String, max_frames: int = 0) -> AnimatedTexture:
	return GifManager.animated_texture_from_file(path, max_frames)
