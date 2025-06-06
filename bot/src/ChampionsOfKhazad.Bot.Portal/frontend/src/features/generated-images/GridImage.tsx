import { Avatar, Box, styled, Typography } from "@mui/joy";
import GeneratedImage from "./generated-image.ts";
import { useCallback, useState } from "react";
import { SxProps } from "@mui/joy/styles/types";

const Container = styled("a")`
  position: relative;
  display: block;
`;

const Image = styled("img")`
  min-height: 140px;
`;

const Footer = styled("div")`
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  color: white;
  padding: 0.5rem;
  display: flex;
  align-items: end;
  margin-bottom: 6px;
  background: linear-gradient(0, rgba(0, 0, 0, 0.5) 0%, rgba(0, 0, 0, 0) 100%);
  height: 100px;
`;

const FooterContent = styled("div")`
  display: flex;
  justify-content: space-between;
  align-items: center;
  flex-grow: 1;
`;

const UserContainer = styled("div")`
  display: flex;
  align-items: center;
  gap: 0.5rem;
`;

const promptOverlayStyles: SxProps = {
  position: "absolute",
  top: 0,
  bottom: 0,
  left: 0,
  right: 0,
  marginBottom: "6px",
  transitionDuration: "0.2s",
  transitionTimingFunction: "ease-out",
};

interface GridImageProps {
  image: GeneratedImage;
  width: number;
}

const GridImage = ({ image, width }: GridImageProps) => {
  const [showPrompt, setShowPrompt] = useState(false);

  const onPointerEnter = useCallback(() => {
    setShowPrompt(true);
  }, []);

  const onPointerLeave = useCallback(() => {
    setShowPrompt(false);
  }, []);

  return (
    <Container
      href={image.url}
      target="_blank"
      onPointerEnter={onPointerEnter}
      onPointerLeave={onPointerLeave}
      sx={{
        overflow: "hidden",
      }}
    >
      <Image
        src={image.url}
        srcSet={image.srcSet}
        alt={image.prompt}
        width={width}
      />
      <Footer>
        <FooterContent>
          <UserContainer>
            <Avatar src={image.user.avatarUrl} alt={image.user.name} />
            <Typography level="body-sm" textColor="white">
              {image.user.name}
            </Typography>
          </UserContainer>
          <Typography level="body-sm" textColor="white">
            {image.timestamp.toLocaleDateString()}
          </Typography>
        </FooterContent>
      </Footer>
      <Box
        sx={{
          ...promptOverlayStyles,
          backgroundColor: "rgba(0, 0, 0, 0.6)",
          transitionProperty: "opacity",
          ...(showPrompt
            ? {
                visibility: "visible",
                opacity: 1,
              }
            : {
                visibility: "hidden",
                opacity: 0,
              }),
        }}
      />
      <Typography
        level="body-xs"
        textColor="white"
        p={1}
        sx={{
          ...promptOverlayStyles,
          transitionProperty: "transform",
          ...(showPrompt
            ? {
                visibility: "visible",
                transform: "translateY(0)",
              }
            : {
                visibility: "hidden",
                transform: "translateY(5%)",
              }),
        }}
      >
        {image.prompt}
      </Typography>
    </Container>
  );
};

export default GridImage;
