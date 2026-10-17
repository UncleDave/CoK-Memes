import { useCallback, useEffect, useRef, useState } from "react";
import { useBlocker } from "react-router";
import {
  getLoreFormSnapshot,
  shouldBlockLoreNavigation,
} from "./unsaved-lore.ts";

const useUnsavedLore = () => {
  const formRef = useRef<HTMLFormElement>(null);
  const initialSnapshot = useRef<string>(undefined);
  const [isDirty, setIsDirty] = useState(false);

  const updateDirty = useCallback(() => {
    if (formRef.current && initialSnapshot.current !== undefined)
      setIsDirty(
        getLoreFormSnapshot(new FormData(formRef.current)) !==
          initialSnapshot.current,
      );
  }, []);

  useEffect(() => {
    const form = formRef.current;
    if (!form) return;
    initialSnapshot.current = getLoreFormSnapshot(new FormData(form));

    // Adding or removing alias/role rows does not emit an input event.
    const observer = new MutationObserver(updateDirty);
    observer.observe(form, { childList: true, subtree: true });
    return () => observer.disconnect();
  }, [updateDirty]);

  useEffect(() => {
    if (!isDirty) return;
    const handleBeforeUnload = (event: BeforeUnloadEvent) => {
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", handleBeforeUnload);
    return () => window.removeEventListener("beforeunload", handleBeforeUnload);
  }, [isDirty]);

  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    shouldBlockLoreNavigation(isDirty, currentLocation, nextLocation),
  );

  return { formRef, isDirty, updateDirty, blocker };
};

export default useUnsavedLore;
